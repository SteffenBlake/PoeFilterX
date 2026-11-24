#:property TargetFramework=net11.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class ImportGameData
{
    private sealed record BaseName(
        string Name,
        string ItemClass,
        string Attributes,
        int DropLevel,
        bool Gear,
        bool Released
    );

    private sealed record CompletionData(
        string Game,
        string Version,
        string Revision,
        string[] Classes,
        (string Name, string Tags)[] Bases
    );

    private static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length == 0 ? Directory.GetCurrentDirectory() : args[0]);
        var output = Path.Combine(root, "src", "PoeFilterX.Engine", "Checking", "Generated");
        Directory.CreateDirectory(output);
        var completions = new List<CompletionData>();
        foreach (var game in new[] { "Poe1", "Poe2" })
        {
            completions.Add(Import(root, output, game));
        }

        WriteCompletions(root, output, completions);
        return 0;
    }

    private static CompletionData Import(
        string root,
        string output,
        string game
    )
    {
        var directory = Path.Combine(root, "external", game.ToLowerInvariant() + "-data");
        var revision = Revision(directory);
        var version = File.ReadAllText(Path.Combine(directory, "exported-version.txt")).Trim();
        using var classes = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "data", "item_classes.json")));
        using var bases = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "data", "base_items.json")));
        var classNames = classes.RootElement.EnumerateObject().ToDictionary(
            item => item.Name, item => item.Value.GetProperty("name").GetString()!);
        var baseNames = bases.RootElement.EnumerateObject()
            .Where(item => !string.IsNullOrWhiteSpace(item.Value.GetProperty("name").GetString()))
            .Select(item => CompletionBase(item.Value, classNames)).ToList();
        if (game == "Poe1")
        {
            using var gems = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "data", "gems_minimal.json")));
            foreach (var gem in gems.RootElement.EnumerateArray())
            {
                var discriminator = gem.GetProperty("discriminator");
                if (discriminator.ValueKind != JsonValueKind.String || !discriminator.GetString()!.StartsWith("alt_", StringComparison.Ordinal))
                {
                    continue;
                }

                var id = gem.GetProperty("base_item").GetProperty("id").GetString()!;
                if (bases.RootElement.TryGetProperty(id, out var item))
                {
                    baseNames.Add(CompletionBase(item, classNames) with { Name = gem.GetProperty("display_name").GetString()! });
                }
            }
        }

        using var mods = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "data", "mods.json")));
        var records = mods.RootElement.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal)
            .Where(item => item.Value.GetProperty("text").ValueKind == JsonValueKind.String
                && (item.Value.GetProperty("generation_type").GetString() is "prefix" or "suffix" or "enchantment"
                    || (item.Value.GetProperty("generation_type").GetString() == "unique"
                        && !string.IsNullOrEmpty(item.Value.GetProperty("name").GetString()))))
            .Select(item => ModifierRow(item.Name, item.Value)).Distinct(StringComparer.Ordinal).ToArray();
        Write(output, game, "ItemModifier", records, revision, version);
        Console.WriteLine($"Imported {game} {version} ({revision}).");
        return new CompletionData(
            game,
            version,
            revision,
            [.. classNames.Values.Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            CompletionNames(baseNames, game)
        );
    }

    private static BaseName CompletionBase(JsonElement item, Dictionary<string, string> classes)
    {
        var tags = item.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToHashSet(StringComparer.Ordinal);
        var classId = item.GetProperty("item_class").GetString()!;
        var itemClass = classes.GetValueOrDefault(classId, classId);
        if (string.IsNullOrWhiteSpace(itemClass))
        {
            itemClass = classId;
        }

        var attributes = new List<string>();
        var armourTag = tags.FirstOrDefault(tag => tag.EndsWith("_armour", StringComparison.Ordinal)
            && tag.Split('_').All(part => part is "str" or "dex" or "int" or "armour"));
        var requirements = item.GetProperty("requirements");
        foreach (var (attribute, label, tag) in new[]
            { ("strength", "Str", "str"), ("dexterity", "Dex", "dex"), ("intelligence", "Int", "int") })
        {
            var required = requirements.ValueKind == JsonValueKind.Object && requirements.GetProperty(attribute).GetInt32() > 0;
            if (armourTag is not null ? armourTag.Split('_').Contains(tag) : required)
            {
                attributes.Add(label);
            }
        }

        var gear = tags.Contains("armour") || tags.Contains("weapon")
            || itemClass is "Quivers" or "Rings" or "Amulets" or "Belts"
            || itemClass is "Wands" or "Staves" or "Sceptres";
        return new BaseName(
            item.GetProperty("name").GetString()!,
            itemClass,
            gear ? string.Join(" ", attributes) : "",
            item.GetProperty("drop_level").GetInt32(),
            gear,
            item.GetProperty("release_state").GetString() == "released"
        );
    }

    private static (string Name, string Tags)[] CompletionNames(List<BaseName> bases, string game)
    {
        // Prefer the current, highest-level base over lower-level variants such as Royale items.
        var candidates = bases.GroupBy(item => (item.Name, item.ItemClass)).Select(group => group
            .OrderByDescending(item => item.Released).ThenByDescending(item => item.DropLevel)
            .ThenBy(item => item.Attributes, StringComparer.Ordinal).First()).ToArray();

        // Equal drop levels share a tier within each class/attribute family.
        var levels = candidates.Where(item => item.Gear && item.Released)
            .GroupBy(item => (item.ItemClass, item.Attributes)).ToDictionary(group => group.Key,
                group => group.Select(item => item.DropLevel).Distinct().OrderDescending().ToArray());

        return [.. candidates.Select(item =>
        {
            var tier = 0;
            if (item.Gear && item.Released)
            {
                tier = Array.IndexOf(levels[(item.ItemClass, item.Attributes)], item.DropLevel) + 1;
                // Jewellery uses broad drop-level bands rather than one tier per level.
                tier = (game, item.ItemClass) switch
                {
                    ("Poe1", "Rings") => item.DropLevel >= 68 ? 1 : item.DropLevel >= 20 ? 2 : 3,
                    ("Poe1", "Amulets") => item.DropLevel >= 68 ? 1 : item.DropLevel >= 10 ? 2 : 3,
                    ("Poe1", "Belts") => item.DropLevel >= 68 ? 1 : 2,
                    ("Poe2", "Rings" or "Amulets" or "Belts") => item.DropLevel >= 40 ? 1 : item.DropLevel >= 20 ? 2 : 3,
                    _ => tier
                };
            }

            var tags = string.Join(" ", new[] { item.ItemClass, item.Attributes, tier == 0 ? "" : $"T{tier}" }
                .Where(value => value.Length > 0));
            return (item.Name, Tags: tags);
        }).Distinct().OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Tags, StringComparer.Ordinal)];
    }

    private static void WriteCompletions(
        string root,
        string output,
        List<CompletionData> games
    )
    {
        var code = new StringBuilder("// <auto-generated/>\n// Generated by build/ImportGameData.cs from pinned RePoE exports.\n");
        foreach (var game in games)
        {
            code.Append("// ").Append(game.Game).Append(' ').Append(game.Version).Append(" @ ").Append(game.Revision).Append('\n');
        }

        code.Append(string.Join("\n", File.ReadAllLines(Path.Combine(root, "build", "GAME-DATA-NOTICE.txt"))
            .Select(line => ("// " + line).TrimEnd()))).Append('\n');
        code.Append("using System.Collections.Immutable;\nnamespace PoeFilterX.Engine.Checking;\npublic static class ItemCompletionData\n{\n");
        foreach (var game in games)
        {
            AppendCompletionList(code, game.Game + "ItemClasses", "string", [.. game.Classes.Select(Quote)]);
            AppendCompletionList(
                code,
                game.Game + "BaseTypes",
                "BaseTypeSuggestion",
                [.. game.Bases.Select(item => $"new({Quote(item.Name)}, {Quote(item.Tags)}, {Quote(game.Game.ToLowerInvariant())})")]
            );
        }

        var classSuggestions = games.SelectMany(game => game.Classes.Select(name => (Name: name, Game: game.Game.ToLowerInvariant())))
            .GroupBy(item => item.Name, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"new({Quote(group.Key)}, {Quote("Item Class")}, {Quote(string.Join(" ", group.Select(item => item.Game)))})");
        AppendCompletionList(code, "ItemClassSuggestions", "BaseTypeSuggestion", [.. classSuggestions]);

        code.Append("public static readonly ImmutableArray<string> ItemClasses = [.. ")
            .Append(string.Join(".Concat(", games.Select(game => game.Game + "ItemClasses")))
            .Append(')', games.Count - 1)
            .Append(".Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];\n");
        code.Append("public static readonly ImmutableArray<BaseTypeSuggestion> BaseTypes = [.. ")
            .Append(string.Join(".Concat(", games.Select(game => game.Game + "BaseTypes")))
            .Append(')', games.Count - 1)
            .Append(".GroupBy(item => (item.Name, item.Tags))")
            .Append(".Select(group => group.First() with { Games = string.Join(\" \", group.Select(item => item.Games).Distinct(StringComparer.Ordinal)) })")
            .Append(".OrderBy(item => item.Name, StringComparer.Ordinal)")
            .Append(".ThenBy(item => item.Tags, StringComparer.Ordinal)];\n}\n");
        WriteIfChanged(Path.Combine(output, "ItemCompletionData.g.cs"), code.ToString());
        WriteSkill(root, games, false);
        WriteSkill(root, games, true);
    }

    private static void AppendCompletionList(
        StringBuilder code,
        string name,
        string type,
        string[] rows
    )
    {
        var chunks = rows.Chunk(128).ToArray();
        code.Append("public static readonly ImmutableArray<").Append(type).Append("> ").Append(name).Append(" = [\n");
        for (var index = 0; index < chunks.Length; index++)
        {
            code.Append(".. ").Append(name).Append(index).Append("(),\n");
        }

        code.Append("];\n");
        for (var index = 0; index < chunks.Length; index++)
        {
            code.Append("private static ").Append(type).Append("[] ").Append(name).Append(index)
                .Append("() => [\n").Append(string.Join(",\n", chunks[index])).Append("\n];\n");
        }
    }

    private static void WriteSkill(
        string root,
        List<CompletionData> games,
        bool baseTypes
    )
    {
        var name = baseTypes ? "poefilterx-basetypes" : "poefilterx-item-classes";
        var subject = baseTypes ? "BaseType" : "Class";
        var directory = Path.Combine(root, "skills", name);
        Directory.CreateDirectory(directory);
        var text = new StringBuilder($"---\nname: {name}\ndescription: \"Look up PoE 1 and PoE 2 {subject} names when writing filter conditions.\"\n---\n\n");
        text.Append("Use the selected game's names verbatim in filter conditions. Quote values containing spaces.\n");
        if (baseTypes)
        {
            text.Append("Find bases by name, item class, attributes, or tier. Str, Dex, and Int identify Strength, Dexterity, and Intelligence. ")
                .Append("T1 identifies the highest tier within an equipment family.\n")
                .Append("Use each matching entry's `name` in a `BaseType` condition.\n\n")
                .Append("`BaseType == \"Conquest Lamellar\";`\n");
        }
        else
        {
            text.Append("Use the class names below in `Class` conditions.\n\n")
                .Append("`Class == \"Rune Daggers\";`\n");
        }

        foreach (var game in games)
        {
            var gameName = game.Game == "Poe1" ? "PoE 1" : "PoE 2";
            text.Append($"\n## {gameName}\n\n");
            if (baseTypes)
            {
                var reference = $"references/{game.Game.ToLowerInvariant()}.json";
                text.Append($"Search [{gameName} bases]({reference}) for the requested base name, item class, attributes, or tier.\n");
                Directory.CreateDirectory(Path.Combine(directory, "references"));
                WriteBaseReference(
                    Path.Combine(directory, reference),
                    [.. game.Bases.Select(item => (item.Name, item.Tags + " " + game.Game.ToLowerInvariant()))]
                );
                continue;
            }

            foreach (var line in game.Classes.Select(name => "\"" + JsonEncodedText.Encode(name, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\""))
            {
                text.Append("- ").Append(line).Append('\n');
            }
        }

        WriteIfChanged(Path.Combine(directory, "SKILL.md"), text.ToString());
    }

    private static void WriteBaseReference(string path, (string Name, string Tags)[] bases)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }))
        {
            writer.WriteStartArray();
            foreach (var (Name, Tags) in bases)
            {
                writer.WriteStartObject();
                writer.WriteString("name", Name);
                writer.WriteString("tags", Tags);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        WriteIfChanged(path, Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }

    private static string ModifierRow(string id, JsonElement item)
    {
        var stats = new List<string>();
        foreach (var line in item.GetProperty("text").GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ranges = new List<string>();
            var unlinked = Links().Replace(line, match => match.Groups[1].Value.Split('|')[^1]);
            var signature = StatTokens().Replace(unlinked, match =>
            {
                var ranged = match.Groups[1].Success;
                var minimum = double.Parse(ranged ? match.Groups[1].Value : match.Value, CultureInfo.InvariantCulture);
                var maximum = ranged ? double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : minimum;
                ranges.Add($"new({Math.Min(minimum, maximum).ToString("R", CultureInfo.InvariantCulture)}d, "
                    + $"{Math.Max(minimum, maximum).ToString("R", CultureInfo.InvariantCulture)}d)");
                return "#";
            });
            signature = signature.Replace("+#", "#", StringComparison.Ordinal).Replace("-#", "#", StringComparison.Ordinal);
            stats.Add($"new({Quote(signature)}, [{string.Join(", ", ranges)}])");
        }

        var kind = item.GetProperty("generation_type").GetString() switch
        {
            "prefix" => "Prefix",
            "suffix" => "Suffix",
            "unique" => "Unique",
            _ => "Enchantment"
        };

        return $"new({Quote(id)}, {Quote(item.GetProperty("name").GetString() ?? "")}, "
            + $"ItemModifierKind.{kind}, [{string.Join(", ", stats)}])";
    }

    [GeneratedRegex(@"\[([^\[\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex Links();

    [GeneratedRegex(@"\(([-+]?\d+(?:\.\d+)?)-([-+]?\d+(?:\.\d+)?)\)|[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex StatTokens();

    private static void Write(
        string output,
        string game,
        string type,
        string[] rows,
        string revision,
        string version
    )
    {
        var notice = File.ReadAllLines(Path.Combine(output, "..", "..", "..", "..", "build", "GAME-DATA-NOTICE.txt"));
        var header = "// Generated from https://github.com/repoe-fork/repoe\n// Export: https://github.com/repoe-fork/"
            + (game == "Poe1" ? "repoe-fork.github.io" : "poe2") + "\n"
            + string.Join("\n", notice.Select(line => ("// " + line).TrimEnd())) + "\n";
        var code = new StringBuilder("// <auto-generated/>\n// RePoE " + game + " " + version + " @ " + revision
            + "\n" + header + "#nullable enable\n"
            + "namespace PoeFilterX.Engine.Checking;\ninternal static partial class GameData\n{\n");
        code.Append("internal const string ").Append(game).Append(type).Append("Version = ")
            .Append(Quote(version)).Append(";\ninternal static ").Append(type).Append("[] ").Append(game)
            .Append(type).Append("s() => [\n");
        var chunks = rows.Chunk(128).ToArray();
        for (var index = 0; index < chunks.Length; index++)
        {
            code.Append(".. ").Append(game).Append(type).Append(index).Append("(),\n");
        }

        code.Append("];\n");
        for (var index = 0; index < chunks.Length; index++)
        {
            code.Append("private static ").Append(type).Append("[] ").Append(game).Append(type)
                .Append(index).Append("() => [\n").Append(string.Join(",\n", chunks[index])).Append("\n];\n");
        }

        code.Append("}\n");
        WriteIfChanged(Path.Combine(output, $"{game}.{type}.g.cs"), code.ToString());
    }

    private static string Quote(string value) => "\"" + JsonEncodedText.Encode(value) + "\"";

    private static string Revision(string directory)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add("rev-parse");
        start.ArgumentList.Add("HEAD");
        using var process = Process.Start(start)!;
        var revision = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Cannot determine the pinned game-data revision.");
        }

        return revision;
    }
}
