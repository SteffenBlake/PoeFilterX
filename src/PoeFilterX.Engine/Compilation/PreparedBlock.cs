using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Compilation;

public sealed record PreparedBlock(
    FilterOperations Operations,
    bool IsRoot,
    bool IsAbstract,
    string Path,
    SourceSpan Span
);
