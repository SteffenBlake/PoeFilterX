using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed record FilterBlockSyntax(
    SourceSpan Span,
    bool IsAbstract,
    ImmutableArray<SyntaxNode> Statements
) : SyntaxNode(Span);
