using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed record DocumentSyntax(SourceSpan Span, ImmutableArray<SyntaxNode> Statements) : SyntaxNode(Span);
