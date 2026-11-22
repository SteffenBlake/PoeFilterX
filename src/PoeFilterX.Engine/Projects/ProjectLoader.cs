using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PoeFilterX.Engine.Projects;

/// <summary>Shared entrypoint loading for build tools and editors. No process-global project state.</summary>
public sealed class ProjectLoader(IProjectSource source, ILogger? logger = null)
{
    public ProjectSnapshot Load(
        string entryPoint,
        CancellationToken cancellationToken = default
    )
    {
        var path = Path.GetFullPath(entryPoint);
        return new ProjectLoadSession(
            source,
            logger ?? NullLogger.Instance,
            cancellationToken
        ).Load(path);
    }
}
