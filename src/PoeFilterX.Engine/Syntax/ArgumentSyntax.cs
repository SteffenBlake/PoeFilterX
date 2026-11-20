namespace PoeFilterX.Engine.Syntax;

public sealed record ArgumentSyntax(SourceSpan Span, string Value, bool IsQuoted) : SyntaxNode(Span);
