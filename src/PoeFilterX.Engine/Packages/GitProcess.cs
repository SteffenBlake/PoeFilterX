using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Packages;

internal static class GitProcess
{
    public static async Task<Result<string>> RunAsync(
        string directory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        Stream? output = null
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        string[] configuration = [
            "protocol.allow=never", "protocol.https.allow=always", "protocol.http.allow=always",
            "protocol.ssh.allow=always", "submodule.recurse=false", "fetch.recurseSubmodules=false",
            "core.hooksPath=" + Path.Combine(directory, "disabled-hooks"),
            "core.attributesFile=" + Path.Combine(directory, "disabled-attributes"),
            "init.templateDir=" + Path.Combine(directory, "empty-template")
        ];
        foreach (var setting in configuration)
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(setting);
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GCM_INTERACTIVE"] = "Never";
        start.Environment["GIT_SSH_COMMAND"] = (Environment.GetEnvironmentVariable("GIT_SSH_COMMAND") ?? "ssh") + " -o BatchMode=yes";
        // Repository-local environment inherited from a parent Git command must not redirect our operations.
        string[] repositoryEnvironment = [
            "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES"
        ];
        foreach (var name in repositoryEnvironment)
        {
            start.Environment.Remove(name);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            using var registration = timeout.Token.Register(() => Kill(process));
            var errors = ReadBoundedAsync(process.StandardError, timeout.Token);
            var stdout = output is null
                ? ReadBoundedAsync(process.StandardOutput, timeout.Token)
                : CopyAsync(process.StandardOutput.BaseStream, output, timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var result = await stdout.ConfigureAwait(false);
            var detail = await errors.ConfigureAwait(false);
            return process.ExitCode == 0
                ? result
                : new ParseError(DiagnosticCodes.RestoreFailure, $"Git failed: {detail.Trim()}");
        }
        catch (Win32Exception)
        {
            return new ParseError(
                DiagnosticCodes.RestoreFailure,
                "Git is required to restore this package. Install Git and ensure it is available on PATH."
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ParseError(
                DiagnosticCodes.RestoreFailure,
                "Git timed out. Check connectivity and configure Git credentials or an SSH agent before restoring."
            );
        }
        finally
        {
            Kill(process);
        }
    }

    private static async Task<string> CopyAsync(
        Stream source,
        Stream target,
        CancellationToken cancellationToken
    )
    {
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        return string.Empty;
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var text = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (text.Length < 16384)
            {
                text.Append(buffer, 0, Math.Min(count, 16384 - text.Length));
            }
        }

        return text.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process has not started, or exited concurrently.
        }
    }
}
