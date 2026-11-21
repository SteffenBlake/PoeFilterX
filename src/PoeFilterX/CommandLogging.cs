using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace PoeFilterX;

internal static class CommandLogging
{
    public static Option<LogLevel> CreateVerbosityOption()
    {
        var option = new Option<LogLevel>("--verbosity", "-v", "--v")
        {
            Description = "Minimum log level: Trace, Debug, Information, Warning, Error, Critical, or None.",
            Arity = ArgumentArity.ExactlyOne,
            DefaultValueFactory = _ => LogLevel.Information
        };
        option.Validators.Add(result =>
        {
            if (!Enum.IsDefined(result.GetValueOrDefault<LogLevel>()))
            {
                result.AddError("Verbosity must be a defined log level.");
            }
        });

        return option;
    }

    public static ILoggerFactory CreateFactory(LogLevel minimumLevel)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.Services.AddSingleton<ConsoleFormatter, CompilerConsoleFormatter>();
            builder.AddConsole(options =>
            {
                options.FormatterName = nameof(CompilerConsoleFormatter);
                options.LogToStandardErrorThreshold = LogLevel.Warning;
            });
        });
    }
}
