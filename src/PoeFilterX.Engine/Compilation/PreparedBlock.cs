using System.Collections.Immutable;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Compilation;

public sealed record PreparedBlock(
    FilterOperations Operations,
    bool IsRoot,
    bool IsAbstract,
    string Path,
    SourceSpan Span
)
{
    public PreparedBlock? Parent { get; init; }

    public FilterOperations Local { get; init; } = new();

    public ImmutableArray<SourceReference> Trace { get; init; } = [];
}
