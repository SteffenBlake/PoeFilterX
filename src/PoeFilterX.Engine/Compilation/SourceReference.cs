using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Compilation;

public sealed record SourceReference(string Path, SourceSpan Span);
