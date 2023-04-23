using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class PoeFilterXCompiler : IPoeFilterXCompiler
    {
        private FilterXConfiguration Config { get; }
        private IFileParser FileParser { get; }

        public PoeFilterXCompiler(FilterXConfiguration config, IFileParser fileParser)
        {
            Config = config;
            FileParser = fileParser;
        }

        public async Task CompileAsync()
        {
            var filter = new Filter();
            try
            {
                var usedPath = Path.GetFullPath(Config.OutputPath());
                Console.WriteLine($"Building from {usedPath}");

                await FileParser.ParseAsync(filter, Config.UsedPath);
            }
            catch (ParserException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                Environment.Exit(1);
            }

            var outputPath = Path.GetFullPath(Config.OutputPath());
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
    }
}
