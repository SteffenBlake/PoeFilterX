using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;
using PoeFilterX.Business.Services.Abstractions;
using PoeFilterX.Extensions;

namespace PoeFilterX
{
    internal static class BuildCmd
    {
        internal static string HelpText =
@"Compiles a Filter file from a .filterx file. If executed from within 
a directory with a single .filterx file present, path does not need to be provided
    Usage: poefilterx build [--p|--path=<path>] [--o|--output=<path>] [--var=val]
    [--p|--path ""path""] - Path to the input .filterx file. If not specified will search for one in executing dir.
    [--o|--output ""path""] - Path to the output .filter file. Default: ""<sourceFileName>.filter""
    [--v|--verbose true] - Enables Verbose output, e.g. 'poefilterx build --v true'
    [--VarName someValue] - Any additional Environmental variables you wish to pass in to consume via %VarName% or etc 
";

        internal static async Task Run(string[] args)
        {
            Console.WriteLine("== PoeFilterX Build Commencing ==");
            Console.WriteLine($"Executing from: '{Environment.CurrentDirectory}'");

            Console.WriteLine("Building Configuration...");

            if (HasInvalidPathArguments(args))
            {
                await Console.Error.WriteLineAsync("Path options require a nonempty value.");
                Environment.Exit(1);
            }

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_")
                .AddCommandLine(args)
                .Build();

            var sourcePath = config["p"] ?? config["path"];
            if (sourcePath != null && Path.GetExtension(sourcePath) != ".filterx")
            {
                await Console.Error.WriteLineAsync("Build input must be a .filterx file.");
                Environment.Exit(1);
            }

            var serviceProvider = ConfigureServices(config);
            var compiler = serviceProvider.GetRequiredService<IPoeFilterXCompiler>();

            Console.WriteLine("Setup complete, commencing build.");

            await compiler.CompileAsync();
        }

        private static bool HasInvalidPathArguments(string[] args)
        {
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                var separator = argument.IndexOf('=');
                var option = (separator < 0 ? argument : argument[..separator]).ToLowerInvariant();
                if (option is not "--p" and not "--path" and not "--o" and not "--output")
                {
                    continue;
                }

                var value = separator >= 0 ? argument[(separator + 1)..]
                    : ++index < args.Length ? args[index] : null;
                if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--"))
                {
                    return true;
                }
            }

            return false;
        }

        private static IServiceProvider ConfigureServices(IConfiguration config)
        {
            return new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton(config.Get<FilterXConfiguration>() ?? new FilterXConfiguration())
                .AddSingleton(new ExecutingContext())

                .AddLazySingleton<IPoeFilterXCompiler, PoeFilterXCompiler>()

                .AddLazySingleton<IPathResolver, PathResolver>()
                .AddLazySingleton<IFileParser, FileParser>()
                .AddLazySingleton<IStreamFetcher, FileStreamFetcher>()

                .AddLazySingleton<ISectionParser, VariableSectionParser>()
                .AddLazySingleton<IVariableStore, VariableStore>()

                .AddLazySingleton<ISectionParser, FilterXParser>()
                .AddLazySingleton<IFilterBlockParser, FilterBlockParser>()
                .AddLazySingleton<IFilterCommandParser, FilterCommandParser>()

                .AddLazySingleton<ISectionParser, StyleSheetParser>()
                .AddLazySingleton<IStyleBlockParser, StyleBlockParser>()
                .AddLazySingleton<IStyleCommandParser, StyleCommandParser>()

                .AddLazySingleton<ArgParser>()

                .BuildServiceProvider();
        }
    }
}
