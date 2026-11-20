namespace PoeFilterX.IntegrationTests.Infrastructure;

internal sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
