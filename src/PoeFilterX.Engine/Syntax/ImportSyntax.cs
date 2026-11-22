namespace PoeFilterX.Engine.Syntax;

public sealed record ImportSyntax(SourceSpan Span, ArgumentSyntax Path) : SyntaxNode(Span);
