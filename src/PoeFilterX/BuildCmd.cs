using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PoeFilterX.Engine.Handlers;
using PoeFilterX.Engine.Requests;

namespace PoeFilterX;

internal static class BuildCmd
{
    internal static Command Create(IReadOnlyDictionary<string, string> variables)
    {
        var command = new Command("build", "Build a .filterx project (the default command).");
        Configure(command, variables);

        return command;
    }

    internal static void Configure(Command command, IReadOnlyDictionary<string, string> variables)
    {
        var path = new Argument<string?>("path")
        {
            Description = "Input .filterx file. Omit to discover a single file in the current directory.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var pathOption = new Option<string?>("--path", "--p")
        {
            Description = "Path to the input .filterx file.",
            Arity = ArgumentArity.ExactlyOne
        };
        var output = new Option<string?>("--output", "--o")
        {
            Description = "Path to the output .filter file.",
            Arity = ArgumentArity.ExactlyOne
        };
        var verbosity = new Option<LogLevel>("--verbosity", "-v", "--v")
        {
            Description = "Minimum log level: Trace, Debug, Information, Warning, Error, Critical, or None.",
            Arity = ArgumentArity.ExactlyOne,
            DefaultValueFactory = _ => LogLevel.Information
        };
        verbosity.Validators.Add(result =>
        {
            if (!Enum.IsDefined(result.GetValueOrDefault<LogLevel>()))
            {
                result.AddError("Verbosity must be a defined log level.");
            }
        });
        command.Arguments.Add(path);
        command.Options.Add(pathOption);
        command.Options.Add(output);
        command.Options.Add(verbosity);
        command.Validators.Add(result =>
        {
            if (result.GetValue(path) is not null && result.GetValue(pathOption) is not null)
            {
                result.AddError("Specify the input path only once.");
            }
        });
        command.SetAction(result =>
        {
            using var logging = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(result.GetValue(verbosity));
                builder.Services.AddSingleton<ConsoleFormatter, CompilerConsoleFormatter>();
                builder.AddConsole(options =>
                {
                    options.FormatterName = nameof(CompilerConsoleFormatter);
                    options.LogToStandardErrorThreshold = LogLevel.Warning;
                });
            });
            var logger = logging.CreateLogger(nameof(BuildHandler));

            return BuildHandler.Handle(new BuildRequest(
                result.GetValue(path) ?? result.GetValue(pathOption),
                result.GetValue(output),
                variables
            ), logger);
        });
    }
}
