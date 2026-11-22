using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PoeFilterX.IntegrationTests.Infrastructure;

/// <summary>A real Git smart-HTTP endpoint, isolated from credentials and the public network.</summary>
internal sealed class GitPackageRepository : IAsyncDisposable
{
    private readonly TestWorkspace workspace = new();
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task serving;
    private readonly List<Task> requests = [];
    private string? submoduleCommit;

    public GitPackageRepository()
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        Url = $"http://127.0.0.1:{port}/repo.git";
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        serving = ServeAsync();
    }

    public string Url { get; }

    public string Root => workspace.Root;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(workspace.PathFor("work"));
        await GitAsync(workspace.Root, "init", "--bare", "repo.git");
        await GitAsync(workspace.PathFor("work"), "init");
        await GitAsync(workspace.PathFor("work"), "config", "user.name", "Package Test");
        await GitAsync(workspace.PathFor("work"), "config", "user.email", "package@example.test");
        await GitAsync(workspace.PathFor("work"), "remote", "add", "origin", workspace.PathFor("repo.git"));
    }

    public void Write(string path, string text) => workspace.Write("work/" + path, text);

    public async Task<string> CommitAsync()
    {
        var directory = workspace.PathFor("work");
        await GitAsync(directory, "add", ".");
        if (submoduleCommit is { } commit)
        {
            await GitAsync(directory, "update-index", "--add", "--cacheinfo", $"160000,{commit},blocked");
        }

        await GitAsync(directory, "commit", "-m", "Package fixture");
        await GitAsync(directory, "push", "origin", "HEAD:refs/heads/main");
        await GitAsync(workspace.Root, "--git-dir=" + workspace.PathFor("repo.git"), "symbolic-ref", "HEAD", "refs/heads/main");
        return (await GitAsync(directory, "rev-parse", "HEAD")).Trim();
    }

    public void WriteLink(string path, string target)
    {
        File.CreateSymbolicLink(workspace.PathFor("work/" + path), target);
    }

    public void AddSubmodule(string commit)
    {
        Write(".gitmodules", "[submodule \"blocked\"]\npath = blocked\nurl = https://packages.invalid/never-fetch.git\n");
        submoduleCommit = commit;
    }

    private static async Task<string> GitAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.That(process.ExitCode, Is.Zero, await error);
        return await output;
    }

    private async Task ServeAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(lifetime.Token);
                requests.Add(RespondAsync(context));
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("http-backend");
        start.Environment["GIT_PROJECT_ROOT"] = workspace.Root;
        start.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
        start.Environment["PATH_INFO"] = context.Request.Url!.AbsolutePath;
        start.Environment["QUERY_STRING"] = context.Request.Url.Query.TrimStart('?');
        start.Environment["REQUEST_METHOD"] = context.Request.HttpMethod;
        start.Environment["CONTENT_TYPE"] = context.Request.ContentType ?? "";
        start.Environment["HTTP_GIT_PROTOCOL"] = context.Request.Headers["Git-Protocol"] ?? "";
        start.Environment["REMOTE_ADDR"] = "127.0.0.1";
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var response = new MemoryStream();
        var reading = process.StandardOutput.BaseStream.CopyToAsync(response);
        await context.Request.InputStream.CopyToAsync(process.StandardInput.BaseStream);
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        await reading;
        Assert.That(process.ExitCode, Is.Zero, await error);
        var bytes = response.ToArray();
        var headerEnd = bytes.AsSpan().IndexOf("\r\n\r\n"u8);
        Assert.That(headerEnd, Is.GreaterThanOrEqualTo(0));
        var headers = Encoding.ASCII.GetString(bytes, 0, headerEnd);
        foreach (var header in headers.Split("\r\n"))
        {
            var colon = header.IndexOf(':');
            var name = header[..colon];
            var value = header[(colon + 1)..].Trim();
            if (name == "Status")
            {
                context.Response.StatusCode = int.Parse(value.AsSpan(0, 3));
                continue;
            }

            context.Response.Headers[name] = value;
        }

        await context.Response.OutputStream.WriteAsync(bytes.AsMemory(headerEnd + 4));
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        await serving;
        await Task.WhenAll(requests);
        listener.Close();
        lifetime.Dispose();
        workspace.Dispose();
    }
}
