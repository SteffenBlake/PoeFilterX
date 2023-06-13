using Microsoft.Extensions.Configuration;
using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Services.Abstractions;
using System.Text.RegularExpressions;

namespace PoeFilterX.Business.Services
{
    public class VariableStore : IVariableStore
    {
        private IConfiguration Config { get; }
        private IDictionary<string, HashSet<string>> Data { get; set; } = new Dictionary<string, HashSet<string>>();


        public VariableStore(IConfiguration config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Track(string key)
        {
            if (!Data.ContainsKey(key))
            {
                Data[key] = new();
            }
        }

        public void Add(string key, string value)
        {
            Track(key);
            _ = Data[key].Add(value);
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
                    if (Data.ContainsKey(keyInner))
                    {
                        value ??= string.Join(' ', Data[keyInner].Select(s => $"\"{s}\""));
                    }

                    value ??= "";

                    var logVal = value.Length < 30 ? value : $"{value[..30]}...";
                    Console.WriteLine($"Injecting Variable '{keyInner}'\n\tVal: '{logVal}'");

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
