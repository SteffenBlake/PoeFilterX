using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PoeFilterX.Engine.Logging;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Requests;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Handlers;

public static class BuildHandler
{
    public static int Handle(BuildRequest request, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        var started = Stopwatch.GetTimestamp();
        CompilerLog.BuildStarting(logger);
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                CompilerLog.ExecutionDirectory(logger, Directory.GetCurrentDirectory());
            }

            var input = ResolveInput(request.Path);
            if (input is ParseError inputError)
            {
                return ReportError(logger, inputError);
            }

            var path = input switch
            {
                string value => value,
                _ => throw new InvalidOperationException("A resolved entrypoint has no path.")
            };
            if (request.Output is { Length: 0 })
            {
                return ReportError(logger, new ParseError(
                    DiagnosticCodes.BuildFailure,
                    "Output path cannot be empty"
                ));
            }

            var target = NormalizePath(request.Output ?? Path.ChangeExtension(path, ".filter"));
            if (target is ParseError outputError)
            {
                return ReportError(logger, outputError);
            }

            var output = target switch
            {
                string value => value,
                _ => throw new InvalidOperationException("A resolved output has no path.")
            };
            using var scope = logger.BeginScope("Build {Input}", path);
            CompilerLog.BuildInput(logger, path);
            var variables = ReadConfiguration(request.Variables);
            CompilerLog.ConfigurationLoaded(logger, variables.Count);
            if (logger.IsEnabled(LogLevel.Trace))
            {
                foreach (var name in variables.Keys)
                {
                    CompilerLog.ConfigurationVariable(logger, name);
                }
            }

            var snapshot = new ProjectLoader(new FileProjectSource(), logger).Load(path);
            CompilerLog.ProjectLoaded(
                logger,
                snapshot.SyntaxTrees.Count,
                snapshot.VariableFiles.Count,
                snapshot.Imports.Length
            );
            var prepared = ProjectCompiler.Prepare(snapshot, variables);
            if (prepared is ParseError preparationError)
            {
                return ReportError(logger, preparationError, snapshot);
            }

            var project = prepared switch
            {
                PreparedProject value => value,
                Deprecated<PreparedProject> value => ReportDeprecation(logger, value),
                _ => throw new InvalidOperationException("A successful preparation has no project.")
            };
            CompilerLog.Publishing(logger, output);
            using var writer = new StreamWriter(output);
            var written = ProjectCompiler.Write(project, writer);
            if (written is ParseError error)
            {
                return ReportError(logger, error, snapshot);
            }

            writer.Flush();
            var blocks = written switch
            {
                int count => count,
                Deprecated<int> value => ReportDeprecation(logger, value),
                _ => throw new InvalidOperationException("A successful write has no block count.")
            };
            CompilerLog.BuildCompleted(
                logger,
                blocks,
                output,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds
            );

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ReportError(logger, new ParseError(DiagnosticCodes.BuildFailure, exception.Message));
        }
    }

    private static Result<string> ResolveInput(string? path)
    {
        if (path is null)
        {
            var candidates = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.filterx");
            if (candidates.Length != 1)
            {
                return new ParseError(DiagnosticCodes.BuildFailure, "Unable to automatically detect a single .filterx entrypoint");
            }

            return candidates[0];
        }

        if (!Path.GetExtension(path).Equals(".filterx", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return new ParseError(DiagnosticCodes.BuildFailure, $"File not found or invalid .filterx path: '{path}'");
        }

        return NormalizePath(path);
    }

    private static Result<string> NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException exception)
        {
            return new ParseError(DiagnosticCodes.BuildFailure, exception.Message);
        }
    }

    private static int ReportError(
        ILogger logger,
        ParseError error,
        ProjectSnapshot? snapshot = null
    )
    {
        if (!logger.IsEnabled(LogLevel.Error))
        {
            return 1;
        }

        using var writer = new StringWriter();
        if (snapshot is not null)
        {
            DiagnosticRenderer.Write(snapshot, error.Diagnostics, writer);
        }
        else
        {
            foreach (var diagnostic in error.Diagnostics)
            {
                writer.WriteLine($"error {diagnostic.Code}: {diagnostic.Message}");
            }
        }

        CompilerLog.Diagnostic(logger, writer.ToString());

        return 1;
    }

    private static T ReportDeprecation<T>(ILogger logger, Deprecated<T> value)
    {
        CompilerLog.DeprecatedSyntax(logger, value.Message ?? "Deprecated syntax.", value.DeprecatedAfterUtc);

        return value.Value;
    }

    private static Dictionary<string, string> ReadConfiguration(IReadOnlyDictionary<string, string>? commandLine)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            var key = (string)item.Key;
            if (key.StartsWith("POEFILTERX_", StringComparison.OrdinalIgnoreCase))
            {
                values[key[11..]] = (string)item.Value!;
            }
        }

        if (commandLine is not null)
        {
            foreach (var item in commandLine)
            {
                values[item.Key] = item.Value;
            }
        }

        return values;
    }
}
