namespace PoeFilterX.Engine.Syntax;

/// <summary>Grammar token name and source range; trivia and invalid input are retained.</summary>
public readonly record struct SyntaxToken(string Kind, SourceSpan Span, bool IsTrivia);
