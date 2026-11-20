using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using PoeFilterX.Engine.Logging;

namespace PoeFilterX;

internal sealed class CompilerConsoleFormatter() : ConsoleFormatter(nameof(CompilerConsoleFormatter))
{
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter
    )
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // Compiler diagnostics already contain severity, locations, excerpts, and import frames.
        if (logEntry.EventId.Id != (int)CompilerLogEvent.Diagnostic)
        {
            textWriter.Write(Enum.GetName(logEntry.LogLevel));
            textWriter.Write(": ");
        }

        textWriter.Write(message);
        if (!message.EndsWith('\n'))
        {
            textWriter.WriteLine();
        }

        if (logEntry.Exception is { } exception)
        {
            textWriter.WriteLine(exception);
        }
    }
}
