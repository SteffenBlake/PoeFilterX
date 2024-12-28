using Microsoft.Extensions.Configuration;
using PoeFilterX.Business.Services;
using System.Text;

namespace PoeFilterX
{
    internal static class ValidateCmd
    {
        internal static string HelpText =
@"Validates a PoE Filter file against an item. If the item matches a given block of
the filter, the associated line # of the block will be returned as well as the text of the matching block
    Usage: poefilterx build [--p|--path=<path>]
    [--p|--path ""path""] - Path to the input .filter file. If not specified will search for one in executing dir.
";
        internal static async Task Run(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("POEFILTERX_")
                .AddCommandLine(args)
                .Build();

            var path = config["p"] ?? config["path"];

            if (path == null)
            {
                var matches = Directory.GetFiles(Environment.CurrentDirectory, "*.filter");
                if (matches.Length == 1)
                {
                    path = matches[0];
                }
            }
            if (path == null)
            {
                Console.Error.WriteLine($"Could not locate file with path '{path}'");
                return;
            }

            if (!File.Exists(path)) {
                Console.Error.WriteLine("Please specify .filter file path via the --path operator (type `poefilterx help validate` for more info!)");
                return;
            }

            Console.WriteLine("Please copy the item via Alt+Ctrl+C, then paste it into this terminal. Press enter to complete the paste.");

            var item = "";
            ConsoleKeyInfo key;
            do
            {
                item +=  (char)Console.Read();
            } while (!item.EndsWith("\r\n\r\n"));
            
            using var filterStream = File.OpenRead(path);
            using var reader = new StreamReader(filterStream, Encoding.UTF8, true);

            var matcher = new FilterMatcher();

            var matchingBlocks = "";

            var isMatch = false;
            var isContinue = false;
            var block = "";
            var lineNumber = 0;
            var show = false;
            while (!reader.EndOfStream) {
                var line = await reader.ReadLineAsync() + "\n";
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) 
                {
                    block += line;
                    continue;
                }

                if (line.StartsWith("Continue")) {
                    isContinue = true;
                    block += line;
                    continue;
                }

                if (line.StartsWith("Show") || line.StartsWith("Hide")) {
                    if (show) {
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                    show = line.StartsWith("Show");
                    if (isMatch) {
                        matchingBlocks += block + "\n";
                        if (!isContinue) {
                            break;
                        }
                    }

                    isMatch = true;
                    block = $"== Match! Line #{lineNumber}: == \n" + line;
                    isContinue = false;
                    continue;
                }

                var matches = matcher.IsMatch(item, line);
                if (show) {
                    Console.Write($"{(matches?"O:":"X:")} {line}");
                }
                isMatch &= matches;
                block += line;
            }

            if (matchingBlocks.Length == 0) {
                if (isMatch) {
                    // Very last block was a match
                    Console.WriteLine(block);
                } else {
                    Console.WriteLine("No matches found!");
                }
            } else {
                Console.WriteLine(matchingBlocks);
            }

            Console.WriteLine("Press any key to finish");
            Console.ReadKey(true);
        }
    }
}
