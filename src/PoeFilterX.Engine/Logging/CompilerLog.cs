using Microsoft.Extensions.Logging;

namespace PoeFilterX.Engine.Logging;

internal static partial class CompilerLog
{
    [LoggerMessage(
        (int)CompilerLogEvent.BuildStarting,
        LogLevel.Information,
        "PoeFilterX build commencing."
    )]
    public static partial void BuildStarting(ILogger logger);

    [LoggerMessage(
        (int)CompilerLogEvent.ExecutionDirectory,
        LogLevel.Debug,
        "Executing from '{Directory}'."
    )]
    public static partial void ExecutionDirectory(ILogger logger, string directory);

    [LoggerMessage(
        (int)CompilerLogEvent.BuildInput,
        LogLevel.Information,
        "Building from '{Input}'."
    )]
    public static partial void BuildInput(ILogger logger, string input);

    [LoggerMessage(
        (int)CompilerLogEvent.ConfigurationLoaded,
        LogLevel.Debug,
        "Loaded {Count} configuration variables."
    )]
    public static partial void ConfigurationLoaded(ILogger logger, int count);

    [LoggerMessage(
        (int)CompilerLogEvent.ProjectLoaded,
        LogLevel.Debug,
        "Loaded {Files} syntax files, {VariableFiles} variable files, and {Imports} imports."
    )]
    public static partial void ProjectLoaded(
        ILogger logger,
        int files,
        int variableFiles,
        int imports
    );

    [LoggerMessage(
        (int)CompilerLogEvent.Publishing,
        LogLevel.Information,
        "Publishing to '{Output}'."
    )]
    public static partial void Publishing(ILogger logger, string output);

    [LoggerMessage(
        (int)CompilerLogEvent.BuildCompleted,
        LogLevel.Information,
        "Published {Blocks} blocks to '{Output}' in {ElapsedMilliseconds:F1} ms."
    )]
    public static partial void BuildCompleted(
        ILogger logger,
        int blocks,
        string output,
        double elapsedMilliseconds
    );

    [LoggerMessage(
        (int)CompilerLogEvent.OpeningFile,
        LogLevel.Debug,
        "Opening '{Path}'."
    )]
    public static partial void OpeningFile(ILogger logger, string path);

    [LoggerMessage(
        (int)CompilerLogEvent.ParsedFile,
        LogLevel.Debug,
        "Parsed '{Path}': {Statements} root statements, {Diagnostics} diagnostics."
    )]
    public static partial void ParsedFile(
        ILogger logger,
        string path,
        int statements,
        int diagnostics
    );

    [LoggerMessage(
        (int)CompilerLogEvent.ImportResolved,
        LogLevel.Trace,
        "Import '{Source}' -> '{Target}'."
    )]
    public static partial void ImportResolved(
        ILogger logger,
        string source,
        string target
    );

    [LoggerMessage(
        (int)CompilerLogEvent.CachedFile,
        LogLevel.Trace,
        "Using cached file '{Path}'."
    )]
    public static partial void CachedFile(ILogger logger, string path);

    [LoggerMessage(
        (int)CompilerLogEvent.VariableLoaded,
        LogLevel.Trace,
        "Loaded variable '{Variable}' with {Count} values from '{Path}'."
    )]
    public static partial void VariableLoaded(
        ILogger logger,
        string variable,
        int count,
        string path
    );

    [LoggerMessage(
        (int)CompilerLogEvent.ConfigurationVariable,
        LogLevel.Trace,
        "Configuration variable '{Variable}' takes precedence over imported values."
    )]
    public static partial void ConfigurationVariable(ILogger logger, string variable);

    [LoggerMessage(
        (int)CompilerLogEvent.DeprecatedSyntax,
        LogLevel.Warning,
        "{Message} Support ends at {DeprecatedAfterUtc:O}."
    )]
    public static partial void DeprecatedSyntax(
        ILogger logger,
        string message,
        DateTime deprecatedAfterUtc
    );

    [LoggerMessage(
        (int)CompilerLogEvent.Diagnostic,
        LogLevel.Error,
        "{Diagnostics}"
    )]
    public static partial void Diagnostic(ILogger logger, string diagnostics);
    [LoggerMessage((int)CompilerLogEvent.PackageCached, LogLevel.Debug, "Using cached package {Alias} at {Commit}")]
    public static partial void PackageCached(
        ILogger logger,
        string alias,
        string commit
    );

    [LoggerMessage((int)CompilerLogEvent.PackageResolving, LogLevel.Information, "Resolving package {Alias}")]
    public static partial void PackageResolving(ILogger logger, string alias);

    [LoggerMessage((int)CompilerLogEvent.PackageRestoring, LogLevel.Information, "Restoring package {Alias} at {Commit}")]
    public static partial void PackageRestoring(
        ILogger logger,
        string alias,
        string commit
    );

    [LoggerMessage((int)CompilerLogEvent.PackagesRestored, LogLevel.Information, "Packages are restored for {Entrypoint}")]
    public static partial void PackagesRestored(ILogger logger, string entrypoint);

}
