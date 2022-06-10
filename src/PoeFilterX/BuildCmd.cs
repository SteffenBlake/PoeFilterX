using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;
using PoeFilterX.Business.Services.Abstractions;
using PoeFilterX.Extensions;
using System.Security.Principal;

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

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_")
                .AddCommandLine(args)
                .Build();

            Console.WriteLine("Building Configuration...");
            var filterXConfig = config.Get<FilterXConfiguration>() ?? new FilterXConfiguration();
            Console.WriteLine("Fetching files...");
            var usedPath = filterXConfig.UsedPath();

            // If no path specified, check if we only have one .filterx file in launch dir
            if (usedPath == null)
            {
                return;
            }

            Console.WriteLine($"Building from {usedPath}");

            var services = new ServiceCollection();
            // Register IConfiguration for fetching of Enviro Variables in parsing
            _ = services.AddSingleton<IConfiguration>(config);
            ConfigureServices(config, services);

            var serviceProvider = services.BuildServiceProvider();
            Console.WriteLine("Setup complete, commencing build.");

            var parser = serviceProvider.GetRequiredService<IFileParser>();

            var filter = new Filter();
            try
            {
                await parser.ParseAsync(filter, usedPath);
            } catch (ParserException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                Environment.Exit(1);
            }

            var outputPath = Path.GetFullPath(filterXConfig.OutputPath());
            Console.WriteLine($"Publishing to {outputPath}");

            // Check if its a directory or file
            if (Directory.Exists(outputPath))
            {
                await Console.Error.WriteLineAsync("Output path is a directory, not a file.");
                Environment.Exit(1);
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            await using var writer = File.OpenWrite(outputPath);
            await using var stream = new StreamWriter(writer);
            try
            {
                await filter.WriteAsync(stream);
            }
            catch (ParserException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                Environment.Exit(1);
            }

            await stream.FlushAsync();
            writer.Flush();
        }

        private static void ConfigureServices(IConfiguration config, IServiceCollection services)
        {
            _ = services.AddSingleton(config);
            _ = services.AddSingleton(new ExecutingContext());

            _ = services
                .AddLazySingleton<IFileParser, FileParser>()
                .AddLazySingleton<IStreamFetcher, FileStreamFetcher>();

            _ = services
                .AddLazySingleton<ISectionParser, FilterParser>()
                .AddLazySingleton<IFilterBlockParser, FilterBlockParser>()
                .AddLazySingleton<IFilterCommandParser, FilterCommandParser>();

            _ = services
                .AddLazySingleton<ISectionParser, StyleSheetParser>()
                .AddLazySingleton<IStyleBlockParser, StyleBlockParser>()
                .AddLazySingleton<IStyleCommandParser, StyleCommandParser>();

            _ = services.AddLazySingleton<ArgParser>();
        }
    }
}
