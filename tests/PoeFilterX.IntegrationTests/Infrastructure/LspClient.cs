using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace PoeFilterX.IntegrationTests.Infrastructure;

internal sealed class LspClient : IAsyncDisposable
{
    private readonly Process process;
    private readonly Stream input;
    private readonly Stream output;
    private readonly Task reading;
    private readonly Task<string> stderr;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> requests = new();
    private readonly Channel<JsonElement> notifications = Channel.CreateUnbounded<JsonElement>();
    private readonly SemaphoreSlim writing = new(1);
    private int nextId;
    private bool exited;

    public LspClient(
        Process process,
        Stream input,
        Stream output
    )
    {
        this.process = process;
        this.input = input;
        this.output = output;
        stderr = process.StandardError.ReadToEndAsync();
        reading = ReadAsync();
    }

    public static Process StartProcess(string directory, params string[] options)
    {
        var cliPath = Environment.GetEnvironmentVariable("PFX_INTEGRATION_CLI_PATH")
            ?? File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "cli-path.txt")).Trim();
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        info.ArgumentList.Add(cliPath);
        info.ArgumentList.Add("language-server");
        foreach (var option in options)
        {
            info.ArgumentList.Add(option);
        }

        return Process.Start(info) ?? throw new InvalidOperationException("Could not start language server.");
    }

    public static LspClient Start(string directory, params string[] options)
    {
        var process = StartProcess(directory, options);
        return new(process, process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
    }

    public async Task<JsonElement> RequestAsync(string method, JsonNode? parameters = null)
    {
        var id = Interlocked.Increment(ref nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        requests[id] = completion;
        try
        {
            await SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters });
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            requests.TryRemove(id, out _);
        }
    }

    public Task NotifyAsync(string method, JsonNode? parameters = null)
    {
        return SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = parameters });
    }

    public async Task<JsonElement> NotificationAsync(string method)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (await notifications.Reader.WaitToReadAsync(timeout.Token))
        {
            if (notifications.Reader.TryRead(out var notification) && notification.GetProperty("method").GetString() == method)
            {
                return notification.GetProperty("params");
            }
        }

        throw new EndOfStreamException("Language server disconnected before the expected notification.");
    }

    private async Task SendAsync(JsonObject message)
    {
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await writing.WaitAsync();
        try
        {
            await input.WriteAsync(header);
            await input.WriteAsync(bytes);
            await input.FlushAsync();
        }
        finally
        {
            writing.Release();
        }
    }

    private async Task ReadAsync()
    {
        try
        {
            while (true)
            {
                var header = new StringBuilder();
                var single = new byte[1];
                while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (await output.ReadAsync(single) == 0)
                    {
                        return;
                    }

                    header.Append((char)single[0]);
                }

                var lengthLine = header.ToString().Split("\r\n").Single(line =>
                    line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                var length = int.Parse(lengthLine.AsSpan("Content-Length:".Length).Trim(), CultureInfo.InvariantCulture);
                var bytes = new byte[length];
                await output.ReadExactlyAsync(bytes);
                using var document = JsonDocument.Parse(bytes);
                var message = document.RootElement.Clone();
                if (message.TryGetProperty("method", out _) && message.TryGetProperty("id", out var serverId))
                {
                    await notifications.Writer.WriteAsync(message);
                    await SendAsync(new JsonObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = serverId.GetInt32(),
                        ["result"] = null
                    });
                    continue;
                }

                if (message.TryGetProperty("id", out var id) && requests.TryGetValue(id.GetInt32(), out var pending))
                {
                    pending.TrySetResult(message);
                    continue;
                }

                await notifications.Writer.WriteAsync(message);
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            foreach (var pending in requests.Values)
            {
                pending.TrySetException(exception);
            }
        }
        finally
        {
            notifications.Writer.TryComplete();
        }
    }

    public async Task<int> ExitAsync(bool shutdown = true)
    {
        if (shutdown)
        {
            var response = await RequestAsync("shutdown");
            Assert.That(response.TryGetProperty("error", out _), Is.False, response.ToString());
        }

        await NotifyAsync("exit");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        exited = true;
        Assert.That(await stderr, Is.Empty);
        return process.ExitCode;
    }

    public async ValueTask DisposeAsync()
    {
        if (!exited && !process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        await reading;
        await input.DisposeAsync();
        await output.DisposeAsync();
        process.Dispose();
        writing.Dispose();
    }
}
