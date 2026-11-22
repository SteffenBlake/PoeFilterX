using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Packages;

public sealed record PackageReference(
    string Alias,
    string Repository,
    string? RequestedCommit,
    SourceSpan Span,
    string? Commit = null,
    string? Root = null
);
