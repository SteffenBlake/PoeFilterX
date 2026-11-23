using System.CommandLine;
using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Handlers;
using PoeFilterX.Engine.Packages;

namespace PoeFilterX;

internal static class RestoreCmd
{
    internal static Command Create()
    {
        var command = new Command("restore", "Restore Git packages without compiling the project.");
        var path = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
        var update = new Option<bool>("--update") { Description = "Refresh unpinned packages to their remote HEAD commits." };
        var locked = new Option<bool>("--locked") { Description = "Require an up-to-date lockfile without changing it." };
        var verbosity = CommandLogging.CreateVerbosityOption();
        CommandConfiguration.AddOptions(command);
        command.Arguments.Add(path);
        command.Options.Add(update);
        command.Options.Add(locked);
        command.Options.Add(verbosity);
        command.Validators.Add(result =>
        {
            if (result.GetValue(update) && result.GetValue(locked))
            {
                result.AddError("--update and --locked cannot be combined.");
            }
        });
        command.SetAction((result, cancellationToken) => CommandConfiguration.InvokeAsync(result, result.GetValue(path), async settings =>
        {
            using var logging = CommandLogging.CreateFactory(settings.Get("verbosity", LogLevel.Information));
            return await RestoreHandler.HandleAsync(
                settings.EntryPoint,
                new RestoreOptions(settings.Get("update", false), settings.Get("locked", false),
                    settings.CacheRoot, Settings: settings),
                logging.CreateLogger(nameof(RestoreHandler)),
                cancellationToken
            );
        }));

        return command;
    }
}
