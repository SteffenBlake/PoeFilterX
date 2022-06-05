using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;
using PoeFilterX.Business.Services.Abstractions;
using PoeFilterX.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoeFilterX
{
    internal static class BuildCmd
    {
        internal static string HelpText =
@"Compiles a Filter file from a .filterx file.
    Usage: PoeFilterX build [--p|--path=<path>] [--o|--output=<path>]
    [--p|--path] - Path to the input .filterx file. Default: "".filterx""
    [--o|--output] - Path to the output .filter file. Default: ""<sourceFileName>.filter""
";

        internal static async Task Run(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_")
                .AddCommandLine(args)
                .Build();

            var filterXConfig = config.Get<FilterXConfiguration>();

            var services = new ServiceCollection();
            // Register IConfiguration for fetching of Enviro Variables in parsing
            services.AddSingleton<IConfiguration>(config);
            ConfigureServices(config, services);

            var serviceProvider = services.BuildServiceProvider();

            var parser = serviceProvider.GetRequiredService<IFileParser>();

            var filter = new Filter();
            try
            {
                await parser.ParseAsync(filter, filterXConfig.UsedPath);
            } catch (ParserException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.Exit(1);
            }

            if (File.Exists(filterXConfig.OutputPath))
                File.Delete(filterXConfig.OutputPath);

            using var writer = File.OpenWrite(filterXConfig.OutputPath);
            using var stream = new StreamWriter(writer);
            await filter.WriteAsync(stream);

            stream.Flush();
            writer.Flush();
        }

        private static void ConfigureServices(IConfiguration config, IServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(config);

            services.AddLazySingleton<IFileParser, FileParser>();
            services.AddLazySingleton<IStreamFetcher, FileStreamFetcher>();

            services.AddLazySingleton<ISectionParser, FilterParser>();
            services.AddLazySingleton<IFilterBlockParser, FilterBlockParser>();
            services.AddLazySingleton<IFilterCommandParser, FilterCommandParser>();

            services.AddLazySingleton<ISectionParser, StyleSheetParser>();
            services.AddLazySingleton<IStyleBlockParser, StyleBlockParser>();
            services.AddLazySingleton<IStyleCommandParser, StyleCommandParser>();

            services.AddLazySingleton<ArgParser>();
        }
    }
}
