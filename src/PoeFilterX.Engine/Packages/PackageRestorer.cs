using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Logging;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Packages;

public static class PackageRestorer
{
    public static async Task<Result<PackageCatalog>> RestoreAsync(
        string entrypoint,
        RestoreOptions? options = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Result<SyntaxTree> parsed;
            using (var stream = File.OpenRead(entrypoint))
            {
                parsed = FilterXParserService.Parse(stream, entrypoint, cancellationToken);
            }

            if (parsed is ParseError error)
            {
                return error;
            }

            var tree = parsed switch
            {
                SyntaxTree value => value,
                Deprecated<SyntaxTree> value => value.Value,
                _ => throw new InvalidOperationException("The package entrypoint has no syntax tree.")
            };

            return await RestoreAsync(tree, options, logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ParseError(
                DiagnosticCodes.RestoreFailure,
                exception.Message).At(entrypoint, default
            );
        }
    }

    public static async Task<Result<PackageCatalog>> RestoreAsync(
        SyntaxTree tree,
        RestoreOptions? options = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!tree.Diagnostics.IsEmpty)
        {
            return new ParseError(tree.Diagnostics, tree);
        }

        var manifest = PackageManifest.Read(tree);
        if (!manifest.Diagnostics.IsEmpty)
        {
            return new ParseError(manifest.Diagnostics, tree);
        }

        options ??= new();
        logger ??= NullLogger.Instance;
        if (options.Locked && options.Update)
        {
            return new ParseError(
                DiagnosticCodes.PackageLock,
                "--locked and --update cannot be combined."
            ).At(tree.Source.Path, default);
        }

        try
        {
            if (manifest.References.IsEmpty)
            {
                return RestoreEmpty(tree.Source.Path, manifest, options);
            }

            var existing = PackageCatalogReader.Read(tree, options.CacheRoot);
            if (options.NoRestore || !options.Update && !existing.NeedsRestore)
            {
                return await RestoreManifestAsync(tree, manifest, options, logger, cancellationToken).ConfigureAwait(false);
            }

            var cache = options.CacheRoot ?? PackageIdentity.CacheRoot;
            var locks = Path.Combine(cache, "locks");
            Directory.CreateDirectory(locks);
            var projectLock = PackageIdentity.RepositoryDirectory(locks, Path.GetFullPath(tree.Source.Path)) + ".lock";
            await using var coordination = await AcquireAsync(projectLock, cancellationToken).ConfigureAwait(false);
            return await RestoreManifestAsync(tree, manifest, options, logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new ParseError(
                DiagnosticCodes.RestoreFailure,
                exception.Message
            ).At(tree.Source.Path, default);
        }
    }

    private static Result<PackageCatalog> RestoreEmpty(
        string entrypoint,
        PackageCatalog manifest,
        RestoreOptions options
    )
    {
        if (!File.Exists(PackageCatalogReader.LockPath(entrypoint)))
        {
            return manifest;
        }

        var read = PackageCatalogReader.ReadLock(entrypoint);
        if (read is ParseError error)
        {
            return error;
        }

        if (read is PackageLock { Packages.IsEmpty: false } && options.Locked)
        {
            return new ParseError(
                DiagnosticCodes.PackageLock,
                "The lockfile still contains removed packages. Run 'poefilterx restore' to update it."
            ).At(entrypoint, default);
        }

        if (!options.Locked && !options.NoRestore)
        {
            WriteLock(entrypoint, new(1, []));
        }

        return manifest;
    }

    private static async Task<Result<PackageCatalog>> RestoreManifestAsync(
        SyntaxTree tree,
        PackageCatalog manifest,
        RestoreOptions options,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var read = PackageCatalogReader.ReadLock(tree.Source.Path);
        if (read is ParseError error)
        {
            return error;
        }

        var locked = read switch
        {
            PackageLock value => value,
            _ => throw new InvalidOperationException("A package lock result has no value.")
        };
        if (options.Locked && (!File.Exists(PackageCatalogReader.LockPath(tree.Source.Path))
            || !Matches(manifest, locked)))
        {
            return new ParseError(
                DiagnosticCodes.PackageLock,
                "The package lockfile is missing or out of date. Run 'poefilterx restore' and commit the updated lockfile."
            ).At(tree.Source.Path, default);
        }

        if (options.NoRestore)
        {
            var existing = PackageCatalogReader.Read(tree, options.CacheRoot);
            return existing.Diagnostics.IsEmpty ? existing : new ParseError(
                [.. existing.Diagnostics.Select(diagnostic => diagnostic with { Severity = DiagnosticSeverity.Error })], tree
            );
        }

        var cache = options.CacheRoot ?? PackageIdentity.CacheRoot;
        var references = ImmutableArray.CreateBuilder<PackageReference>(manifest.References.Length);
        foreach (var reference in manifest.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previous = locked.Packages.FirstOrDefault(entry => Matches(reference, entry));
            var commit = reference.RequestedCommit ?? (options.Update ? null : previous?.Commit);
            var resolved = await RestoreReferenceAsync(
                reference,
                commit,
                cache,
                logger,
                cancellationToken
            ).ConfigureAwait(false);
            if (resolved is ParseError restoreError)
            {
                return restoreError.At(tree.Source.Path, reference.Span) with { PartialTree = tree };
            }

            if (resolved is PackageReference restored)
            {
                references.Add(restored);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = references.MoveToImmutable();
        if (!options.Locked)
        {
            WriteLock(tree.Source.Path, new(1, [.. result.Select(reference => new PackageLock.Entry(
                reference.Alias, reference.Repository, reference.RequestedCommit, reference.Commit!
            ))]));
        }

        return new PackageCatalog(result, []);
    }

    private static async Task<Result<PackageReference>> RestoreReferenceAsync(
        PackageReference reference,
        string? commit,
        string cache,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var directory = PackageIdentity.RepositoryDirectory(cache, reference.Repository);
        if (commit is not null && PackageSnapshot.IsComplete(Path.Combine(directory, commit), commit))
        {
            CompilerLog.PackageCached(logger, reference.Alias, commit);
            return reference with { Commit = commit, Root = Path.Combine(directory, commit, "files") };
        }

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "empty-template"));
        await using var cacheLock = await AcquireAsync(Path.Combine(directory, "restore.lock"), cancellationToken).ConfigureAwait(false);
        if (commit is null)
        {
            CompilerLog.PackageResolving(logger, reference.Alias);
            var head = await GitProcess.RunAsync(
                directory,
                ["ls-remote", "--exit-code", "--", reference.Repository, "HEAD"],
                cancellationToken
            ).ConfigureAwait(false);
            if (head is ParseError error)
            {
                return error;
            }

            commit = head is string text ? text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() : null;
            if (commit is null || !PackageIdentity.IsCommit(commit))
            {
                return new ParseError(
                    DiagnosticCodes.RestoreFailure,
                    "The repository does not advertise a HEAD commit. Pin an explicit commit or configure the repository's default branch."
                );
            }

            commit = commit.ToLowerInvariant();
        }

        CompilerLog.PackageRestoring(logger, reference.Alias, commit);
        var snapshot = await PackageSnapshot.CreateAsync(
            reference.Repository,
            commit,
            directory,
            cancellationToken
        ).ConfigureAwait(false);
        return snapshot switch
        {
            string root => reference with { Commit = commit, Root = root },
            ParseError error => error,
            _ => throw new InvalidOperationException("A restored package has no snapshot.")
        };
    }

    private static bool Matches(PackageCatalog manifest, PackageLock locked)
    {
        return locked.Packages.Length == manifest.References.Length
            && manifest.References.All(reference => locked.Packages.Any(entry => Matches(reference, entry)));
    }

    private static bool Matches(PackageReference reference, PackageLock.Entry entry)
    {
        return entry.Alias == reference.Alias && entry.Repository == reference.Repository
            && entry.RequestedCommit == reference.RequestedCommit
            && (reference.RequestedCommit is null || entry.Commit == reference.RequestedCommit);
    }

    private static async Task<FileStream> AcquireAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void WriteLock(string entrypoint, PackageLock value)
    {
        var path = PackageCatalogReader.LockPath(entrypoint);
        var text = JsonSerializer.Serialize(value, PackageJsonContext.Default.PackageLock) + Environment.NewLine;
        if (File.Exists(path) && File.ReadAllText(path) == text)
        {
            return;
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
