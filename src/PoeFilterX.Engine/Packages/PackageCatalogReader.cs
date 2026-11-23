using System.Collections.Immutable;
using PoeFilterX.Engine.Configuration;
using System.Text.Json;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Packages;

public static class PackageCatalogReader
{
    public static string LockPath(string entrypoint) => entrypoint + ".lock.json";

    public static PackageCatalog Read(
        SyntaxTree tree,
        string? cacheRoot = null,
        ProjectSettings? settings = null
    )
    {
        var manifest = PackageManifest.Read(tree);
        if (manifest.References.IsEmpty)
        {
            return manifest;
        }

        if (settings is null)
        {
            var configured = ProjectSettings.Load(Path.GetDirectoryName(tree.Source.Path)!);
            if (configured is ParseError configurationError)
            {
                return manifest with { Diagnostics = [.. manifest.Diagnostics, .. configurationError.Diagnostics] };
            }

            settings = (ProjectSettings)configured.Value!;
        }

        var read = ReadLock(tree.Source.Path);
        if (read is ParseError error)
        {
            return manifest with { Diagnostics = [.. manifest.Diagnostics, .. error.Diagnostics] };
        }

        var locked = read switch
        {
            PackageLock value => value,
            _ => throw new InvalidOperationException("A package lock result has no value.")
        };
        var cache = cacheRoot ?? settings.CacheRoot;
        var overrides = settings.PackageOverrides;
        var references = manifest.References.Select(reference => overrides.TryGetValue(reference.Repository, out var local)
            ? reference with { Root = local, Commit = reference.RequestedCommit, IsOverride = true }
            : Resolve(reference, locked, cache));
        var resolved = references.ToImmutableArray();
        var warnings = resolved.Where(reference => reference.Root is null).Select(reference => new SyntaxDiagnostic(
            DiagnosticCodes.RestoreRequired,
            $"Package '@{reference.Alias}' is not restored. Run 'poefilterx restore' for this entrypoint.",
            tree.Source.Path,
            reference.Span
        )
        { Severity = DiagnosticSeverity.Warning });

        return new(resolved, [.. manifest.Diagnostics, .. warnings]);
    }

    internal static Result<PackageLock> ReadLock(string entrypoint)
    {
        var path = LockPath(entrypoint);
        try
        {
            if (!File.Exists(path))
            {
                return new PackageLock(1, []);
            }

            using var stream = File.OpenRead(path);
            var value = JsonSerializer.Deserialize(stream, PackageJsonContext.Default.PackageLock);
            if (value is null || value.Version != 1 || value.Packages.IsDefault
                || value.Packages.Any(entry => !ValidEntry(entry))
                || value.Packages.Select(entry => entry.Alias).Distinct(StringComparer.Ordinal).Count() != value.Packages.Length)
            {
                return new ParseError(
                    DiagnosticCodes.PackageLock,
                    "Invalid or unsupported package lockfile."
                ).At(path, default);
            }

            return value with
            {
                Packages = [.. value.Packages.Select(entry => entry with
            {
                Commit = entry.Commit.ToLowerInvariant(),
                RequestedCommit = entry.RequestedCommit?.ToLowerInvariant()
            })]
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ParseError(
                DiagnosticCodes.PackageLock,
                $"Cannot read package lockfile: {exception.Message}"
            ).At(path, default);
        }
    }

    private static bool ValidEntry(PackageLock.Entry? entry)
    {
        return entry is not null && PackageIdentity.IsCommit(entry.Commit ?? "")
            && PackageIdentity.IsAlias(entry.Alias ?? "") && PackageIdentity.IsRepository(entry.Repository ?? "")
            && (entry.RequestedCommit is null || PackageIdentity.IsCommit(entry.RequestedCommit)
                && entry.RequestedCommit.Equals(entry.Commit, StringComparison.OrdinalIgnoreCase));
    }

    private static PackageReference Resolve(
        PackageReference reference,
        PackageLock locked,
        string cacheRoot
    )
    {
        var entry = locked.Packages.FirstOrDefault(entry => entry.Alias == reference.Alias
            && entry.Repository == reference.Repository && entry.RequestedCommit == reference.RequestedCommit);
        var commit = reference.RequestedCommit ?? entry?.Commit;
        if (commit is null)
        {
            return reference;
        }

        var snapshot = PackageIdentity.SnapshotDirectory(cacheRoot, reference.Repository, commit);
        try
        {
            return reference with
            {
                Commit = commit,
                Root = PackageSnapshot.IsComplete(snapshot, commit) ? Path.Combine(snapshot, "files") : null
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return reference with { Commit = commit };
        }
    }
}
