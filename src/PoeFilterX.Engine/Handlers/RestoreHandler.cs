using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Packages;
using PoeFilterX.Engine.Logging;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Handlers;

public static class RestoreHandler
{
    public static async Task<int> HandleAsync(
        string? input,
        RestoreOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default
    )
    {
        var resolved = EntrypointResolver.Resolve(input, options.Settings);
        if (resolved is ParseError inputError)
        {
            return BuildHandler.ReportError(logger, inputError);
        }

        var path = resolved switch
        {
            string value => value,
            _ => throw new InvalidOperationException("The entrypoint has no path.")
        };
        var restored = await PackageRestorer.RestoreAsync(path, options, logger, cancellationToken).ConfigureAwait(false);
        if (restored is ParseError error)
        {
            return BuildHandler.ReportError(logger, error, new ProjectLoader(new FileProjectSource()).Load(path, cancellationToken));
        }

        CompilerLog.PackagesRestored(logger, path);
        return 0;
    }
}
