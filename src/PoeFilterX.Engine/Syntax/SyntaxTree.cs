using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed record SyntaxTree(
    SourceText Source,
    DocumentSyntax Root,
    ImmutableArray<SyntaxToken> Tokens,
    ImmutableArray<SyntaxDiagnostic> Diagnostics
);
