using System.Collections.Immutable;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Packages;

public static class PackageManifest
{
    public static PackageCatalog Read(SyntaxTree tree)
    {
        var references = ImmutableArray.CreateBuilder<PackageReference>();
        var errors = ImmutableArray.CreateBuilder<SyntaxDiagnostic>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in tree.Root.Statements.OfType<PackageImportSyntax>())
        {
            var syntaxErrors = tree.Diagnostics.Where(error =>
                error.Span.Start >= node.Span.Start && error.Span.Start <= node.Span.End);
            if (syntaxErrors.Any() || !tree.Source.Slice(node.Span).EndsWith(";", StringComparison.Ordinal))
            {
                errors.AddRange(syntaxErrors);
                continue;
            }

            var message = Validate(node, names);
            if (message is not null)
            {
                errors.Add(new(DiagnosticCodes.InvalidPackage, message, tree.Source.Path, node.Span));
                continue;
            }

            references.Add(new(
                node.Alias.Value, node.Repository.Value, node.Commit?.Value.ToLowerInvariant(), node.Span
            ));
        }

        foreach (var block in tree.Root.Statements.OfType<FilterBlockSyntax>())
        {
            errors.AddRange(NestedImports(block).Select(node => new SyntaxDiagnostic(
                DiagnosticCodes.PackageScope,
                "Package imports must appear at the entrypoint's top level.",
                tree.Source.Path,
                node.Span
            )));
        }

        return new(references.ToImmutable(), errors.ToImmutable());
    }

    public static IEnumerable<PackageImportSyntax> NestedImports(FilterBlockSyntax block)
    {
        return block.Statements.OfType<PackageImportSyntax>()
            .Concat(block.Statements.OfType<FilterBlockSyntax>().SelectMany(NestedImports));
    }

    private static string? Validate(PackageImportSyntax node, HashSet<string> names)
    {
        if (node.Alias.IsQuoted || !PackageIdentity.IsAlias(node.Alias.Value))
        {
            return "Expected a package alias starting with a letter or underscore, followed by letters, digits, dots, underscores, or hyphens.";
        }

        if (!node.Repository.IsQuoted || !PackageIdentity.IsRepository(node.Repository.Value))
        {
            return "Expected a quoted HTTP, HTTPS, or SSH Git repository URL, without HTTP credentials, passwords, queries, or fragments.";
        }

        if (node.Commit is { } commit && !PackageIdentity.IsCommit(commit.Value))
        {
            return "Package pins must be full Git commit IDs (40 or 64 hexadecimal characters).";
        }

        return names.Add(node.Alias.Value) ? null : $"Package alias '{node.Alias.Value}' is declared more than once.";
    }
}
