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
        command.SetAction(async (result, cancellationToken) =>
        {
            using var logging = CommandLogging.CreateFactory(result.GetValue(verbosity));
            return await RestoreHandler.HandleAsync(
                result.GetValue(path),
                new RestoreOptions(result.GetValue(update), result.GetValue(locked)),
                logging.CreateLogger(nameof(RestoreHandler)),
                cancellationToken
            );
        });

        return command;
    }
}
