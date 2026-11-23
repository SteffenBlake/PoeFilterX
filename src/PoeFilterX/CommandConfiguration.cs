using System.CommandLine;
using CommandResult = System.CommandLine.Parsing.CommandResult;
using System.Globalization;
using System.Text.Json;
using PoeFilterX.Engine.Configuration;
using PoeFilterX.Engine.Results;

namespace PoeFilterX;

internal static class CommandConfiguration
{
    public static void AddOptions(Command command)
    {
        command.Options.Add(new Option<string?>("--profile") { Description = "Load .poefilterx.<profile>.json after the base configuration." });
        command.Options.Add(new Option<string?>("--package-overrides") { Description = "JSON object mapping Git repository URLs to local package directories." });
        command.Options.Add(new Option<string?>("--package-cache") { Description = "Package cache directory." });
    }

    public static async Task<int> InvokeAsync(
        ParseResult result,
        string? path,
        Func<ProjectSettings, Task<int>> action
    )
    {
        try
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var commands = new Stack<Command>();
            for (var current = result.CommandResult; current is not null; current = current.Parent as CommandResult)
            {
                commands.Push(current.Command);
            }

            foreach (var option in commands.SelectMany(command => command.Options))
            {
                if (result.GetResult(option) is not { Implicit: false } supplied)
                {
                    continue;
                }

                var name = Key(option.Name);
                var value = supplied.GetValueOrDefault<object>();
                if (name == "packageOverrides")
                {
                    ProjectSettings.AddPackageOverrides(values, (string)value!);
                }
                else if (value is string[] items)
                {
                    for (var index = 0; index < items.Length; index++)
                    {
                        values[$"{name}:{index}"] = items[index];
                    }
                }
                else
                {
                    values[name] = Convert.ToString(value, CultureInfo.InvariantCulture);
                }
            }

            if (result.CommandResult.Command.Name is not "init")
            {
                path ??= values.GetValueOrDefault("path");
                if (path is not null && !ProjectSettings.IsConfigurationFile(path))
                {
                    values["entrypoint"] = Path.GetFullPath(path);
                }
            }

            foreach (var key in new[] { "output", "packageCache", "itemFile", "path" })
            {
                if (values.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                {
                    values[key] = Path.GetFullPath(value);
                }
            }

            var profile = values.GetValueOrDefault("profile");
            var loaded = ProjectSettings.LoadForInput(path, profile, values);
            if (loaded is ParseError error)
            {
                foreach (var diagnostic in error.Diagnostics)
                {
                    Console.Error.WriteLine($"{diagnostic.Path}: error {diagnostic.Code}: {diagnostic.Message}");
                }

                return 1;
            }

            return await action((ProjectSettings)loaded.Value!);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or FormatException or JsonException)
        {
            Console.Error.WriteLine($"error PFX0001: Invalid configuration: {exception.Message}");
            return 1;
        }
    }

    private static string Key(string option)
    {
        var words = option.TrimStart('-').Split('-');
        return words[0] + string.Concat(words.Skip(1).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
