using Microsoft.Extensions.Configuration;
using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PoeFilterX.Business.Services
{
    public class VariableStore : IVariableStore
    {
        private IConfiguration Config { get; }
        private IDictionary<string, List<string>> Data { get; set; } = new Dictionary<string, List<string>>();

        public string FileExtension => ".json";

        public VariableStore(IConfiguration config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task ParseAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null)
        {
            var newDataRaw = await reader.ReadToEndAsync();

            var newData = JsonSerializer.Deserialize<Dictionary<string, string[]>>(newDataRaw) ?? new();

            foreach (var kvp in newData)
            {
                var args = kvp.Value.SelectMany(s => s.ToArgs()).ToList();
                if (!Data.ContainsKey(kvp.Key))
                {
                    Data[kvp.Key] = kvp.Value.ToList();
                }
                else
                {
                    Data[kvp.Key].AddRange(kvp.Value);
                }

                // Final Distinct cleanup
                Data[kvp.Key] = Data[kvp.Key].Distinct().ToList();
            }
        }

        public string[] InjectEnvironment(IReadOnlyList<string> args)
        {
            return InjectEnvironmentInternal(args).ToArray();
        }

        private IEnumerable<string> InjectEnvironmentInternal(IReadOnlyList<string> args)
        {
            var enviroRegex = new Regex("%\\w+%");
            for (var n = 0; n < args.Count; n++)
            {
                var dirty = false;
                var arg = args[n];
                while (enviroRegex.IsMatch(arg))
                {
                    dirty = true;
                    var match = enviroRegex.Matches(arg)[0];
                    var key = arg.Substring(match.Index, match.Length);
                    var keyInner = key[1..^1];
                    var value = Config[keyInner];
                    if (value == null && Data.ContainsKey(keyInner))
                    {
                        value = string.Join(' ', Data[keyInner].Select(s => $"\"{s}\""));
                    }
                    if (value == null)
                    {
                        throw new ParserException($"Unrecognized environment variable '{keyInner}'");
                    }
                    else
                    {
                        var logVal = value.Length < 30 ? value : $"{value[..30]}...";
                        Console.WriteLine($"Injecting Variable '{keyInner}'\n\tVal: '{logVal}'");
                    }

                    arg = arg.Replace(key, value);
                }

                if (dirty)
                {
                    foreach (var subArg in arg.ToArgs())
                    {
                        yield return subArg;
                    }
                }
                else
                {
                    yield return arg;
                }
            }
        }
    }
}
