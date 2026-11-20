using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PoeFilterX.Engine.Projects;

/// <summary>Shared entrypoint loading for build tools and editors. No process-global project state.</summary>
public sealed class ProjectLoader(IProjectSource source, ILogger? logger = null)
{
    public ProjectSnapshot Load(
        string entryPoint,
        string? projectRoot = null,
        CancellationToken cancellationToken = default
    )
    {
        var path = Path.GetFullPath(entryPoint);
        var root = Path.GetFullPath(projectRoot ?? Path.GetDirectoryName(path)!);

        return new ProjectLoadSession(
            source,
            root,
            logger ?? NullLogger.Instance,
            cancellationToken
        ).Load(path);
    }
}
