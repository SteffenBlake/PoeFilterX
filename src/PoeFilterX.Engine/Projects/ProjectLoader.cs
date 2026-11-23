using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PoeFilterX.Engine.Configuration;
using PoeFilterX.Engine.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace PoeFilterX.Engine.Projects;

/// <summary>Shared entrypoint loading for build tools and editors. No process-global project state.</summary>
public sealed class ProjectLoader(
    IProjectSource source,
    ILogger? logger = null,
    ProjectSettings? settings = null
)
{
    public ProjectSnapshot Load(
        string entryPoint,
        CancellationToken cancellationToken = default
    )
    {
        var path = Path.GetFullPath(entryPoint);
        var configured = settings is null
            ? ProjectSettings.Load(Path.GetDirectoryName(path)!) : new Result<ProjectSettings>(settings);
        var resolved = configured.Value as ProjectSettings
            ?? new ProjectSettings(Path.GetDirectoryName(path)!, null, new ConfigurationBuilder().Build());
        var project = new ProjectLoadSession(
            source,
            logger ?? NullLogger.Instance,
            resolved,
            cancellationToken
        ).Load(path);

        return configured is ParseError error
            ? project with { Diagnostics = [.. error.Diagnostics, .. project.Diagnostics] } : project;
    }
}
