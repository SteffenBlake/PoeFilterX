using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Parsing.Generated;
using ItemGrammarParser = PoeFilterX.Engine.Parsing.Generated.CopiedItemParser;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Checking;

public static partial class CopiedItemParser
{
    public const int MaximumTextLength = 1024 * 1024;

    private static readonly FrozenDictionary<string, FilterFact> NumberFields = new Dictionary<string, FilterFact>
    {
        ["Item Level"] = FilterFact.ItemLevel,
        ["Quality"] = FilterFact.Quality,
        ["Stack Size"] = FilterFact.StackSize,
        ["Map Tier"] = FilterFact.MapTier,
        ["Waystone Tier"] = FilterFact.WaystoneTier,
        ["Memory Strands"] = FilterFact.MemoryStrands,
        ["Unidentified Item Tier"] = FilterFact.UnidentifiedItemTier
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly ImmutableDictionary<FilterFact, string> FlagLines = new Dictionary<FilterFact, string>
    {
        [FilterFact.Corrupted] = "Corrupted",
        [FilterFact.Mirrored] = "Mirrored"
    }.ToImmutableDictionary();

    private static readonly ImmutableArray<(string Text, FilterFact? Fact)> NamePrefixes =
    [
        ("Superior ", null),
        ("Blight-ravaged ", FilterFact.UberBlightedMap),
        ("Blighted ", FilterFact.BlightedMap),
        ("Synthesised ", FilterFact.SynthesisedItem),
        ("Shaped ", FilterFact.ShapedMap),
        ("Fractured ", FilterFact.FracturedItem),
        ("Fated ", FilterFact.Fated),
        ("Foulborn ", FilterFact.Foulborn),
        ("Replica ", FilterFact.Replica),
        ("Anomalous ", FilterFact.AlternateQuality),
        ("Divergent ", FilterFact.AlternateQuality),
        ("Phantasmal ", FilterFact.AlternateQuality)
    ];

    public static Result<ItemFacts> Parse(
        string text,
        GameVersion game,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(game) || text.Length is 0 or > MaximumTextLength)
        {
            return new ParseError(DiagnosticCodes.InvalidItem, "Supply a copied item of at most 1 MiB and select PoE 1 or PoE 2.");
        }

        text = text.Trim('\uFEFF', '\r', '\n') + "\n";
        var lexer = new CopiedItemLexer(new AntlrInputStream(text));
        lexer.RemoveErrorListeners();
        var parser = new ItemGrammarParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        var diagnostics = new List<SyntaxDiagnostic>();
        parser.AddErrorListener(new CopiedItemErrorListener(diagnostics));
        var document = parser.document();
        if (diagnostics.Count > 0)
        {
            return new ParseError([.. diagnostics]);
        }

        var itemClass = document.ITEM_CLASS().GetText()[12..].Trim();
        var rarityText = document.RARITY().GetText()[8..].Trim();
        if (!Enum.TryParse<FilterRarity>(rarityText, out var rarity) || !Enum.IsDefined(rarity))
        {
            if (rarityText is not ("Currency" or "Gem" or "Divination Card" or "Quest"))
            {
                return new ParseError(DiagnosticCodes.InvalidItem, $"Unrecognized item rarity '{rarityText}'.");
            }

            rarity = FilterRarity.Normal;
        }

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var header = document.LINE().Select(line => line.GetText().Trim()).ToArray();
        if (header[^1].StartsWith("You cannot use", StringComparison.Ordinal))
        {
            header = [.. header, .. document.bodyLine().TakeWhile(line => line.SEPARATOR() is null)
                .Select(line => line.GetText().Trim())];
        }

        header = [.. header.Where(line => !line.StartsWith("You cannot use", StringComparison.Ordinal))];
        if (header.Length == 0)
        {
            return new ParseError(DiagnosticCodes.InvalidItem, "The copied item does not contain an item name.");
        }

        var name = header[0];
        var label = header[^1];
        var baseLabel = BaseLabel(label, rarityText, out var prefixFacts);
        var baseType = rarity == FilterRarity.Magic && header.Length == 1
            ? MagicBaseLabel(baseLabel, lines) : baseLabel;

        return ReadFacts(game, name, baseType, itemClass, rarity, prefixFacts, lines, text);
    }

    private static string BaseLabel(
        string label,
        string rarity,
        out ImmutableHashSet<FilterFact> facts
    )
    {
        var prefixes = ImmutableHashSet.CreateBuilder<FilterFact>();
        while (true)
        {
            var (Text, Fact) = NamePrefixes.FirstOrDefault(prefix => IsNamePrefix(prefix.Fact, rarity)
                && label.StartsWith(prefix.Text, StringComparison.Ordinal));
            if (Text is null || label.Length == Text.Length)
            {
                break;
            }

            label = label[Text.Length..];
            if (Fact is { } fact)
            {
                prefixes.Add(fact);
            }
        }

        facts = prefixes.ToImmutable();

        return label;
    }

    private static bool IsNamePrefix(FilterFact? fact, string rarity)
    {
        return fact switch
        {
            FilterFact.AlternateQuality => rarity == "Gem",
            FilterFact.Replica or FilterFact.Foulborn => rarity == "Unique",
            null => rarity is "Normal" or "Magic" or "Rare" or "Unique" or "Gem",
            _ => rarity is "Normal" or "Magic" or "Rare" or "Unique"
        };
    }

    private static string? MagicBaseLabel(string label, string[] lines)
    {
        if (lines.Contains("Unidentified", StringComparer.Ordinal))
        {
            return label;
        }

        // Advanced copies identify affix boundaries; ordinary magic labels do not.
        var affixes = lines.Where(line => line.StartsWith('{') && (line.Contains("Prefix Modifier", StringComparison.Ordinal)
            || line.Contains("Suffix Modifier", StringComparison.Ordinal))).ToArray();
        if (affixes.Length == 0)
        {
            return null;
        }

        foreach (var line in affixes)
        {
            var match = QuotedName().Match(line);
            if (!match.Success)
            {
                return null;
            }

            var affix = match.Groups[1].Value;
            if (line.Contains("Prefix Modifier", StringComparison.Ordinal) && label.StartsWith(affix + " ", StringComparison.Ordinal))
            {
                label = label[(affix.Length + 1)..];
            }
            else if (line.Contains("Suffix Modifier", StringComparison.Ordinal) && label.EndsWith(" " + affix, StringComparison.Ordinal))
            {
                label = label[..^(affix.Length + 1)];
            }
            else
            {
                return null;
            }
        }

        return label;
    }

    private static Result<ItemFacts> ReadFacts(
        GameVersion game,
        string name,
        string? baseType,
        string itemClass,
        FilterRarity rarity,
        ImmutableHashSet<FilterFact> prefixFacts,
        string[] lines,
        string text
    )
    {
        var flags = ImmutableDictionary.CreateBuilder<FilterFact, bool>();
        var numbers = ImmutableDictionary.CreateBuilder<FilterFact, int>();
        var strings = ImmutableDictionary.CreateBuilder<FilterFact, ImmutableArray<string>>();
        foreach (var (fact, marker) in FlagLines)
        {
            flags[fact] = lines.Contains(marker, StringComparer.Ordinal);
        }

        flags[FilterFact.Identified] = !lines.Contains("Unidentified", StringComparer.Ordinal);
        flags[FilterFact.Replica] = prefixFacts.Contains(FilterFact.Replica) || name.StartsWith("Replica ", StringComparison.Ordinal);
        flags[FilterFact.BlightedMap] = prefixFacts.Contains(FilterFact.BlightedMap);
        flags[FilterFact.UberBlightedMap] = prefixFacts.Contains(FilterFact.UberBlightedMap);
        flags[FilterFact.SynthesisedItem] = lines.Contains("Synthesised Item", StringComparer.Ordinal)
            || prefixFacts.Contains(FilterFact.SynthesisedItem);
        flags[FilterFact.FracturedItem] = text.Contains("(fractured)", StringComparison.Ordinal)
            || prefixFacts.Contains(FilterFact.FracturedItem);
        if (text.Contains("(implicit)", StringComparison.Ordinal) || text.Contains("Implicit Modifier", StringComparison.Ordinal))
        {
            flags[FilterFact.HasImplicitMod] = true;
        }

        flags[FilterFact.AnyEnchantment] = text.Contains("(enchant)", StringComparison.Ordinal)
            || text.Contains("Enchantment Modifier", StringComparison.Ordinal);
        if (prefixFacts.Contains(FilterFact.Fated) || rarity != FilterRarity.Unique || game == GameVersion.Poe2)
        {
            flags[FilterFact.Fated] = prefixFacts.Contains(FilterFact.Fated);
        }

        if (prefixFacts.Contains(FilterFact.AlternateQuality))
        {
            flags[FilterFact.AlternateQuality] = true;
        }

        var crucible = text.Contains("Crucible Passive", StringComparison.Ordinal);
        if (crucible || game == GameVersion.Poe2)
        {
            flags[FilterFact.HasCruciblePassiveTree] = crucible;
        }

        flags[FilterFact.Scourged] = text.Contains("(scourge)", StringComparison.Ordinal)
            || text.Contains("Scourge Modifier", StringComparison.Ordinal);
        flags[FilterFact.Imbued] = lines.Contains("Imbued", StringComparer.Ordinal);
        flags[FilterFact.Foulborn] = prefixFacts.Contains(FilterFact.Foulborn) || lines.Contains("Foulborn", StringComparer.Ordinal);
        flags[FilterFact.ZanaMemory] = text.Contains("Zana's Memory", StringComparison.Ordinal);
        flags[FilterFact.ElderItem] = lines.Contains("Elder Item", StringComparer.Ordinal);
        flags[FilterFact.ShaperItem] = lines.Contains("Shaper Item", StringComparer.Ordinal);
        flags[FilterFact.ElderMap] = lines.Any(line => line.StartsWith("Map Tier: ", StringComparison.Ordinal))
            && flags[FilterFact.ElderItem];
        flags[FilterFact.ShapedMap] = prefixFacts.Contains(FilterFact.ShapedMap);
        numbers[FilterFact.Quality] = 0;
        numbers[FilterFact.StackSize] = 1;
        numbers[FilterFact.LinkedSockets] = 0;
        numbers[FilterFact.GemLevel] = 0;
        numbers[FilterFact.MapTier] = 0;
        numbers[FilterFact.WaystoneTier] = 0;
        numbers[FilterFact.MemoryStrands] = 0;
        numbers[FilterFact.UnidentifiedItemTier] = 0;
        numbers[FilterFact.EnchantmentPassiveNum] = 0;
        if (baseType is not null)
        {
            strings[FilterFact.BaseType] = [baseType];
        }

        strings[FilterFact.Class] = [itemClass];
        strings[FilterFact.Sockets] = [];
        strings[FilterFact.SocketGroup] = [];
        if (ReadProperties(lines, numbers, strings) is { } error)
        {
            return error;
        }

        ReadModifiers(lines, flags[FilterFact.Identified], rarity, numbers, strings);
        ReadDefences(lines, numbers);
        var influences = Enum.GetValues<InfluenceType>().Where(influence => influence != InfluenceType.None
            && lines.Contains($"{influence} Item", StringComparer.Ordinal)).ToImmutableArray();

        return new ItemFacts(game, name, baseType, itemClass, rarity, flags.ToImmutable(),
            numbers.ToImmutable(), strings.ToImmutable(), influences)
        { CopiedText = text };
    }

    private static ParseError? ReadProperties(
        string[] lines,
        ImmutableDictionary<FilterFact, int>.Builder numbers,
        ImmutableDictionary<FilterFact, ImmutableArray<string>>.Builder strings
    )
    {
        var requirements = false;
        foreach (var line in lines)
        {
            if (line == "--------")
            {
                requirements = false;
            }

            if (line.StartsWith("Requires", StringComparison.Ordinal) || line == "Requirements:")
            {
                requirements = true;
            }

            var field = NumericField().Match(line);
            if (!requirements && field.Success
                && ReadNumber(field.Groups[1].Value, field.Groups[2].Value, numbers) is { } error)
            {
                return error;
            }

            var separator = line.IndexOf(':');
            if (!requirements && !field.Success && separator > 0 && NumberFields.ContainsKey(line[..separator]))
            {
                return new ParseError(DiagnosticCodes.InvalidItem, $"Invalid numeric item property: {line}");
            }

            if (line.StartsWith("Sockets: ", StringComparison.Ordinal))
            {
                var groups = line[9..].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();
                strings[FilterFact.SocketGroup] = groups;
                strings[FilterFact.Sockets] = [string.Join('-', groups)];
                numbers[FilterFact.LinkedSockets] = groups.IsEmpty ? 0 : groups.Max(group => group.Count(char.IsLetter));
            }

            if (line.StartsWith("Adds ", StringComparison.Ordinal) && line.Contains(" Passive Skills", StringComparison.Ordinal)
                && int.TryParse(line.AsSpan(5, line.IndexOf(' ', 5) - 5), out var passives))
            {
                numbers[FilterFact.EnchantmentPassiveNum] = passives;
            }
        }

        return null;
    }

    private static ParseError? ReadNumber(
        string name,
        string text,
        ImmutableDictionary<FilterFact, int>.Builder numbers
    )
    {
        if (!int.TryParse(text, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return new ParseError(DiagnosticCodes.InvalidItem, $"Item property '{name}' is outside the supported integer range.");
        }

        if (NumberFields.TryGetValue(name, out var fact))
        {
            numbers[fact] = value;
        }
        else if (name == "Level")
        {
            numbers[FilterFact.GemLevel] = value;
        }

        return null;
    }

    private static void ReadDefences(
        string[] lines,
        ImmutableDictionary<FilterFact, int>.Builder numbers
    )
    {
        foreach (var (label, fact) in new[] { ("Armour", FilterFact.BaseArmour), ("Evasion Rating", FilterFact.BaseEvasion),
            ("Energy Shield", FilterFact.BaseEnergyShield), ("Ward", FilterFact.BaseWard) })
        {
            var raw = lines.FirstOrDefault(line => line.StartsWith(label + ": ", StringComparison.Ordinal));
            if (raw is null || raw.Contains("augmented", StringComparison.Ordinal) || numbers[FilterFact.Quality] != 0)
            {
                continue;
            }

            var match = NumericField().Match(raw);
            if (match.Success && int.TryParse(match.Groups[2].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
            {
                numbers[fact] = value;
            }
        }
    }

    private static void ReadModifiers(
        string[] lines,
        bool identified,
        FilterRarity rarity,
        ImmutableDictionary<FilterFact, int>.Builder numbers,
        ImmutableDictionary<FilterFact, ImmutableArray<string>>.Builder strings
    )
    {
        var explicitNames = lines.Where(line => line.StartsWith('{') && (line.Contains("Prefix Modifier", StringComparison.Ordinal)
            || line.Contains("Suffix Modifier", StringComparison.Ordinal)
            || line.Contains("Unique Modifier", StringComparison.Ordinal))).Select(line => QuotedName().Match(line))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToImmutableArray();
        if (!explicitNames.IsEmpty || !identified || rarity == FilterRarity.Normal)
        {
            strings[FilterFact.HasExplicitMod] = explicitNames;
        }

        var enchants = lines.Where(line => line.EndsWith("(enchant)", StringComparison.Ordinal)).ToImmutableArray();
        strings[FilterFact.EnchantmentPassiveNode] = enchants;
        // Filter HasEnchantment uses internal enchantment names, not displayed stat descriptions.
        if (enchants.IsEmpty && !lines.Any(line => line.Contains("Enchantment Modifier", StringComparison.Ordinal)))
        {
            strings[FilterFact.HasEnchantment] = [];
        }

        if (!lines.Any(line => line.Contains("Corruption Implicit Modifier", StringComparison.Ordinal)))
        {
            if (!lines.Contains("Corrupted", StringComparer.Ordinal))
            {
                numbers[FilterFact.CorruptedMods] = 0;
            }
        }
        else
        {
            numbers[FilterFact.CorruptedMods] = lines.Count(line => line.Contains("Corruption Implicit Modifier", StringComparison.Ordinal));
        }

        ReadEldritchTier(lines, "Eater of Worlds", FilterFact.HasEaterOfWorldsImplicit, numbers);
        ReadEldritchTier(lines, "Searing Exarch", FilterFact.HasSearingExarchImplicit, numbers);
    }

    private static void ReadEldritchTier(
        string[] lines,
        string name,
        FilterFact fact,
        ImmutableDictionary<FilterFact, int>.Builder numbers
    )
    {
        var line = lines.FirstOrDefault(line => line.Contains(name + " Implicit Modifier", StringComparison.Ordinal));
        if (line is null)
        {
            if (!lines.Any(line => line.Contains("(implicit)", StringComparison.Ordinal)))
            {
                numbers[fact] = 0;
            }

            return;
        }

        var match = EldritchTier().Match(line);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var tier))
        {
            numbers[fact] = 7 - tier;
        }
    }

    [GeneratedRegex(@"^([A-Za-z][A-Za-z ]*)(?: \([^)]*\))?: ([+]?\d[\d,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericField();

    [GeneratedRegex("\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedName();

    [GeneratedRegex(@"\(Tier: (\d)\)", RegexOptions.CultureInvariant)]
    private static partial Regex EldritchTier();
}
