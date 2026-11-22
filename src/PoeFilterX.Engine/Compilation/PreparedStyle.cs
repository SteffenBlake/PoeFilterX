using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Compilation;

public sealed record PreparedStyle(string Name, StyleOperations Operations)
{
    public string Path { get; init; } = "";

    public SourceSpan Span { get; init; }

    public SourceSpan StyleSpan { get; init; }

    public string Declaration { get; init; } = "";
}
