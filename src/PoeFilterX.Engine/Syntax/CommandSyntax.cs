using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed record CommandSyntax(
    SourceSpan Span,
    string Name,
    SourceSpan NameSpan,
    ImmutableArray<ArgumentSyntax> Arguments
) : SyntaxNode(Span);
