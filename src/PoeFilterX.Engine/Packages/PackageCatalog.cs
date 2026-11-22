using System.Collections.Immutable;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Packages;

public sealed record PackageCatalog(
    ImmutableArray<PackageReference> References,
    ImmutableArray<SyntaxDiagnostic> Diagnostics
)
{
    public static PackageCatalog Empty { get; } = new([], []);

    public bool NeedsRestore => References.Any(reference => reference.Root is null);
}
