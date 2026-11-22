using System.Formats.Tar;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Packages;

internal static class PackageSnapshot
{
    public static async Task<Result<string>> CreateAsync(
        string repository,
        string commit,
        string directory,
        CancellationToken cancellationToken
    )
    {
        var destination = Path.Combine(directory, commit);
        var files = Path.Combine(destination, "files");
        if (IsComplete(destination, commit))
        {
            return files;
        }

        var objects = Path.Combine(directory, commit.Length == 64 ? "objects-sha256.git" : "objects.git");
        if (!File.Exists(Path.Combine(objects, "HEAD")))
        {
            var format = commit.Length == 64 ? "sha256" : "sha1";
            var init = await GitProcess.RunAsync(
                directory,
                ["init", "--bare", "--object-format=" + format, objects],
                cancellationToken
            ).ConfigureAwait(false);
            if (init is ParseError error)
            {
                return error;
            }
        }

        var fetched = await GitProcess.RunAsync(
            objects,
            ["fetch", "--no-tags", "--no-recurse-submodules", "--depth=1", "--", repository, commit],
            cancellationToken
        ).ConfigureAwait(false);
        if (fetched is ParseError fetchError)
        {
            return fetchError;
        }

        var verified = await GitProcess.RunAsync(
            objects,
            ["rev-parse", "--verify", commit + "^{commit}"],
            cancellationToken
        ).ConfigureAwait(false);
        if (verified is ParseError verifyError)
        {
            return verifyError;
        }

        if (verified is not string resolved || resolved.Trim() != commit)
        {
            return new ParseError(
                DiagnosticCodes.RestoreFailure,
                "The fetched Git object does not match the requested commit."
            );
        }

        return await ExtractAsync(objects, destination, commit, cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsComplete(string snapshot, string commit)
    {
        var marker = Path.Combine(snapshot, "complete");
        return Directory.Exists(Path.Combine(snapshot, "files")) && File.Exists(marker)
            && !PackagePaths.HasLink(marker, snapshot) && !PackagePaths.HasLink(Path.Combine(snapshot, "files"), snapshot)
            && File.ReadAllText(marker) == commit;
    }

    private static async Task<Result<string>> ExtractAsync(
        string objects,
        string destination,
        string commit,
        CancellationToken cancellationToken
    )
    {
        var staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(staging);
        try
        {
            var archive = Path.Combine(staging, "archive.tar");
            await using (var output = File.Create(archive))
            {
                var archived = await GitProcess.RunAsync(
                    objects,
                    ["archive", "--format=tar", commit],
                    cancellationToken,
                    output
                ).ConfigureAwait(false);
                if (archived is ParseError archiveError)
                {
                    return archiveError;
                }
            }

            var root = Path.Combine(staging, "files");
            Directory.CreateDirectory(root);
            var extracted = Extract(archive, root, cancellationToken);
            if (extracted is ParseError error)
            {
                return error;
            }

            File.Delete(archive);
            File.WriteAllText(Path.Combine(staging, "complete"), commit);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
            }

            if (Directory.Exists(destination))
            {
                DeleteIncomplete(destination);
            }

            Directory.Move(staging, destination);
            return Path.Combine(destination, "files");
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                DeleteIncomplete(staging);
            }
        }
    }

    private static void DeleteIncomplete(string directory)
    {
        if (new DirectoryInfo(directory).LinkTarget is not null)
        {
            Directory.Delete(directory);
            return;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(directory, "*", options))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, true);
    }

    private static Result<bool> Extract(
        string archive,
        string root,
        CancellationToken cancellationToken
    )
    {
        using var input = File.OpenRead(archive);
        using var reader = new TarReader(input);
        while (reader.GetNextEntry() is { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.EntryType == TarEntryType.GlobalExtendedAttributes)
            {
                continue;
            }

            var path = Path.GetFullPath(entry.Name, root);
            if (entry.Name.Contains('\\') || entry.Name.Contains(':') || !PackagePaths.IsWithin(path, root)
                || entry.Name.Split('/').Contains(".git", StringComparer.OrdinalIgnoreCase))
            {
                return new ParseError(
                    DiagnosticCodes.RestoreFailure,
                    "The repository contains an unsafe archive path."
                );
            }

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(path);
                continue;
            }

            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                return new ParseError(
                    DiagnosticCodes.RestoreFailure,
                    "Packages cannot contain symbolic links, hard links, or special files."
                );
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            entry.DataStream?.CopyTo(output);
        }

        return true;
    }
}
