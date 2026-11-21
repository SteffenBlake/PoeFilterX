namespace PoeFilterX.Engine.Syntax;

public sealed record PackageImportSyntax(
    SourceSpan Span,
    ArgumentSyntax Alias,
    ArgumentSyntax Repository,
    ArgumentSyntax? Commit
) : SyntaxNode(Span);
