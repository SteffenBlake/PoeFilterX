using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed record StyleSyntax(
    SourceSpan Span,
    string Name,
    SourceSpan NameSpan,
    ImmutableArray<CommandSyntax> Declarations
) : SyntaxNode(Span);
