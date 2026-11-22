using Microsoft.Extensions.Logging;

namespace PoeFilterX.Engine.Tests.Logging;

internal sealed class RecordingLogger : ILogger
{
    internal sealed record Entry(
        LogLevel Level,
        EventId Event,
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> Properties
    );

    public List<Entry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        IReadOnlyList<KeyValuePair<string, object?>> properties = state is IReadOnlyList<KeyValuePair<string, object?>> values
            ? values : [];
        Entries.Add(new(logLevel, eventId, formatter(state, exception), properties));
    }
}
