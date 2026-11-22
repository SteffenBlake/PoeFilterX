using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Packages;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Projects;

public static class CompilerProjectLoader
{
    public static async Task<ProjectSnapshot> LoadAsync(
        string path,
        RestoreOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default
    )
    {
        var loader = new ProjectLoader(new FileProjectSource(), logger);
        var snapshot = loader.Load(path, cancellationToken);
        if (snapshot.SyntaxTrees.TryGetValue(path, out var entrypoint)
            && (!snapshot.Packages.References.IsEmpty || File.Exists(PackageCatalogReader.LockPath(path))))
        {
            var restored = await PackageRestorer.RestoreAsync(entrypoint, options, logger, cancellationToken).ConfigureAwait(false);
            if (restored is ParseError error)
            {
                return snapshot with { Diagnostics = error.Diagnostics };
            }

            if (snapshot.Packages.NeedsRestore)
            {
                snapshot = loader.Load(path, cancellationToken);
            }
        }

        return snapshot;
    }
}
