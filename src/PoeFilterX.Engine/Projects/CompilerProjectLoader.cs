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
        var loader = new ProjectLoader(new FileProjectSource(), logger, options.Settings);
        var snapshot = loader.Load(path, cancellationToken);
        if (snapshot.SyntaxTrees.TryGetValue(path, out var entrypoint)
            && (!snapshot.Packages.References.IsEmpty || File.Exists(PackageCatalogReader.LockPath(path))))
        {
            options = options with
            {
                Settings = snapshot.Settings,
                CacheRoot = options.CacheRoot ?? snapshot.Settings?.CacheRoot,
                Locked = options.Locked || snapshot.Settings?.Get("locked", false) == true,
                NoRestore = options.NoRestore || snapshot.Settings?.Get("noRestore", false) == true,
                Update = options.Update || snapshot.Settings?.Get("update", false) == true
            };
            var restored = await PackageRestorer.RestoreAsync(entrypoint, options, logger, cancellationToken).ConfigureAwait(false);
            if (restored is ParseError error)
            {
                return snapshot with { Diagnostics = error.Diagnostics };
            }

            if (snapshot.Packages.NeedsRestore || options.Update)
            {
                snapshot = loader.Load(path, cancellationToken);
            }
        }

        return snapshot;
    }
}
