using System.Diagnostics;
using NUnit.Framework;

namespace PoeFilterX.IntegrationTests.Infrastructure;

internal static class CliRunner
{
    public static async Task<CliResult> RunAsync(
        string workingDirectory,
        string[] arguments,
        string? input = null,
        IReadOnlyDictionary<string, string>? environment = null
    )
    {
        var recordedPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "cli-path.txt");
        var cliPath = Environment.GetEnvironmentVariable("PFX_INTEGRATION_CLI_PATH")
            ?? File.ReadAllText(recordedPath).Trim();
        Assert.That(File.Exists(cliPath), Is.True, $"CLI artifact not found: {cliPath}");
        var start = new ProcessStartInfo
        {
            FileName = cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : cliPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (cliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(cliPath);
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var variable in environment ?? new Dictionary<string, string>())
        {
            start.Environment[variable.Key] = variable.Value;
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start CLI");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (input != null)
        {
            await process.StandardInput.WriteAsync(input);
        }

        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"CLI timed out.\nstdout:\n{await stdout}\nstderr:\n{await stderr}");
        }

        var result = new CliResult(process.ExitCode, await stdout, await stderr);
        TestContext.WriteLine($"Arguments: {string.Join(" | ", arguments)}");
        TestContext.WriteLine(
            $"Exit: {result.ExitCode}\nstdout:\n{result.StandardOutput}\nstderr:\n{result.StandardError}"
        );
        return result;
    }
}
