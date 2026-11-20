using System.CommandLine;
using PoeFilterX.Engine.Handlers;
using PoeFilterX.Engine.Requests;

namespace PoeFilterX;

internal static class BuildCmd
{
    internal static Command Create()
    {
        var command = new Command("build", "Build a .filterx project (the default command).");
        Configure(command);

        return command;
    }

    internal static void Configure(Command command)
    {
        var path = new Option<string?>("--path", "--p")
        {
            Description = "Path to the input .filterx file.",
            Arity = ArgumentArity.ExactlyOne
        };
        var output = new Option<string?>("--output", "--o")
        {
            Description = "Path to the output .filter file.",
            Arity = ArgumentArity.ExactlyOne
        };
        var verbose = new Option<bool>("--verbose", "--v")
        {
            Description = "Enable verbose output."
        };
        command.Options.Add(path);
        command.Options.Add(output);
        command.Options.Add(verbose);
        command.SetAction(result => BuildHandler.Handle(new BuildRequest(
            result.GetValue(path),
            result.GetValue(output),
            result.GetValue(verbose)
        )));
    }
}
