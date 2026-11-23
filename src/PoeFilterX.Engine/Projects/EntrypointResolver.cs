using PoeFilterX.Engine.Configuration;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Projects;

public static class EntrypointResolver
{
    public static Result<string> Resolve(string? path, ProjectSettings? settings = null)
    {
        if (settings is null)
        {
            var configured = ProjectSettings.LoadForInput(path);
            if (configured is ParseError error)
            {
                return error;
            }

            settings = (ProjectSettings)configured.Value!;
        }

        if (path is not null && ProjectSettings.IsConfigurationFile(path))
        {
            path = null;
        }

        path ??= settings.EntryPoint;
        if (path is null)
        {
            var candidates = EntrypointDiscovery.Find(settings.Root);
            if (candidates.Length != 1)
            {
                var detail = candidates.IsEmpty
                    ? "No entrypoint.filterx or *.entrypoint.filterx file was found in the current directory."
                    : $"Multiple entrypoints were found: {string.Join(", ", candidates)}.";

                return new ParseError(
                    DiagnosticCodes.BuildFailure,
                    $"Unable to automatically detect a single entrypoint. {detail} Specify an input file: poefilterx <path>."
                );
            }

            return candidates[0];
        }

        if (!Path.GetExtension(path).Equals(".filterx", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return new ParseError(DiagnosticCodes.BuildFailure, $"File not found or invalid .filterx path: '{path}'");
        }

        return NormalizePath(path);
    }

    private static Result<string> NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException exception)
        {
            return new ParseError(DiagnosticCodes.BuildFailure, exception.Message);
        }
    }

}
