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
        var label = header[^1];
        var catalog = ItemBaseCatalog.For(game);
        var affixes = rarity == FilterRarity.Magic || label.StartsWith("Superior ", StringComparison.Ordinal)
            || label.StartsWith("Blighted ", StringComparison.Ordinal) || label.StartsWith("Blight-ravaged ", StringComparison.Ordinal)
            || label.StartsWith("Synthesised ", StringComparison.Ordinal) || label.StartsWith("Shaped ", StringComparison.Ordinal);
        var itemBase = catalog.Find(label, itemClass, affixes);
        if (itemBase is null && header.Any(line => line.StartsWith("You cannot use", StringComparison.Ordinal)))
        {
            itemBase = lines.Skip(2).Select(line => catalog.Find(line, itemClass, rarity == FilterRarity.Magic)).FirstOrDefault(value => value is not null);
        }

        if (itemBase is null)
        {
            return new ParseError(DiagnosticCodes.UnknownItemBase,
                $"Item base '{label}' was not found for {game}. Check the game selection and the copied item text.");
        }

        var name = header[0];
        if (name.StartsWith("You cannot use", StringComparison.Ordinal))
        {
            var baseIndex = Array.IndexOf(lines, itemBase.Name);
            name = rarity is FilterRarity.Rare or FilterRarity.Unique && baseIndex > 0 && lines[baseIndex - 1] != "--------"
                ? lines[baseIndex - 1] : itemBase.Name;
        }

        return ReadFacts(game, name, label, itemBase, rarity, lines, text);
    }

    private static Result<ItemFacts> ReadFacts(
        GameVersion game,
        string name,
        string label,
        ItemBase itemBase,
        FilterRarity rarity,
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
        flags[FilterFact.Replica] = name.StartsWith("Replica ", StringComparison.Ordinal);
        flags[FilterFact.BlightedMap] = label.StartsWith("Blighted ", StringComparison.Ordinal);
        flags[FilterFact.UberBlightedMap] = label.StartsWith("Blight-ravaged ", StringComparison.Ordinal);
        flags[FilterFact.SynthesisedItem] = lines.Contains("Synthesised Item", StringComparer.Ordinal)
            || label.StartsWith("Synthesised ", StringComparison.Ordinal);
        flags[FilterFact.FracturedItem] = text.Contains("(fractured)", StringComparison.Ordinal)
            || text.Contains("Fractured ", StringComparison.Ordinal);
        flags[FilterFact.HasImplicitMod] = itemBase.HasImplicit || text.Contains("(implicit)", StringComparison.Ordinal)
            || text.Contains("Implicit Modifier", StringComparison.Ordinal);
        flags[FilterFact.AnyEnchantment] = text.Contains("(enchant)", StringComparison.Ordinal)
            || text.Contains("Enchantment Modifier", StringComparison.Ordinal);
        flags[FilterFact.AlternateQuality] = itemBase.Tags.Contains("alternate_quality");
        flags[FilterFact.TransfiguredGem] = itemBase.Tags.Contains("transfigured_gem");
        if (rarity != FilterRarity.Unique || game == GameVersion.Poe2 || itemBase.Tags.Contains("fated_unique"))
        {
            flags[FilterFact.Fated] = itemBase.Tags.Contains("fated_unique");
        }

        var crucible = text.Contains("Crucible Passive", StringComparison.Ordinal);
        if (crucible || game == GameVersion.Poe2 || (!itemBase.Tags.Contains("weapon") && !itemBase.Tags.Contains("shield")))
        {
            flags[FilterFact.HasCruciblePassiveTree] = crucible;
        }

        flags[FilterFact.Scourged] = text.Contains("(scourge)", StringComparison.Ordinal)
            || text.Contains("Scourge Modifier", StringComparison.Ordinal);
        flags[FilterFact.Imbued] = text.Contains("Imbued", StringComparison.Ordinal);
        flags[FilterFact.Foulborn] = text.Contains("Foulborn", StringComparison.Ordinal);
        flags[FilterFact.ZanaMemory] = text.Contains("Zana's Memory", StringComparison.Ordinal);
        flags[FilterFact.ElderItem] = lines.Contains("Elder Item", StringComparer.Ordinal);
        flags[FilterFact.ShaperItem] = lines.Contains("Shaper Item", StringComparer.Ordinal);
        flags[FilterFact.ElderMap] = itemBase.ItemClass == "Maps" && flags[FilterFact.ElderItem];
        flags[FilterFact.ShapedMap] = label.StartsWith("Shaped ", StringComparison.Ordinal);
        numbers[FilterFact.Width] = itemBase.Width;
        numbers[FilterFact.Height] = itemBase.Height;
        numbers[FilterFact.DropLevel] = itemBase.DropLevel;
        numbers[FilterFact.Quality] = 0;
        numbers[FilterFact.StackSize] = 1;
        if (itemBase.Domain != "item")
        {
            numbers[FilterFact.ItemLevel] = 0;
        }
        numbers[FilterFact.LinkedSockets] = 0;
        numbers[FilterFact.GemLevel] = 0;
        numbers[FilterFact.MapTier] = 0;
        numbers[FilterFact.WaystoneTier] = 0;
        numbers[FilterFact.MemoryStrands] = 0;
        numbers[FilterFact.UnidentifiedItemTier] = 0;
        numbers[FilterFact.EnchantmentPassiveNum] = 0;
        strings[FilterFact.BaseType] = [itemBase.Name];
        strings[FilterFact.Class] = [itemBase.ItemClass];
        strings[FilterFact.Sockets] = [];
        strings[FilterFact.SocketGroup] = [];
        if (ReadProperties(lines, itemBase, numbers, strings) is { } error)
        {
            return error;
        }

        ReadModifiers(lines, flags[FilterFact.Identified], rarity, numbers, strings);
        ReadDefences(itemBase, lines, numbers);
        var influences = Enum.GetValues<InfluenceType>().Where(influence => influence != InfluenceType.None
            && lines.Contains($"{influence} Item", StringComparer.Ordinal)).ToImmutableArray();

        return new ItemFacts(game, name, itemBase.Name, itemBase.ItemClass, rarity, flags.ToImmutable(),
            numbers.ToImmutable(), strings.ToImmutable(), influences)
        { CopiedText = text, ModifierDomain = itemBase.Domain };
    }

    private static ParseError? ReadProperties(
        string[] lines,
        ItemBase itemBase,
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
                && ReadNumber(field.Groups[1].Value, field.Groups[2].Value, itemBase, numbers) is { } error)
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
        ItemBase itemBase,
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
        else if (name == "Level" && itemBase.ItemClass.Contains("Gem", StringComparison.Ordinal))
        {
            numbers[FilterFact.GemLevel] = value;
        }

        return null;
    }

    private static void ReadDefences(
        ItemBase itemBase,
        string[] lines,
        ImmutableDictionary<FilterFact, int>.Builder numbers
    )
    {
        var types = new[] { (itemBase.Armour, "Armour", FilterFact.BaseArmour), (itemBase.Evasion, "Evasion Rating", FilterFact.BaseEvasion),
            (itemBase.EnergyShield, "Energy Shield", FilterFact.BaseEnergyShield), (itemBase.Ward, "Ward", FilterFact.BaseWard) };
        foreach (var (range, label, fact) in types)
        {
            if (range is null)
            {
                numbers[fact] = 0;
                continue;
            }

            if (range.Minimum == range.Maximum)
            {
                numbers[fact] = range.Minimum;
                continue;
            }

            var raw = lines.FirstOrDefault(line => line.StartsWith(label + ": ", StringComparison.Ordinal));
            if (raw is not null && !raw.Contains("augmented", StringComparison.Ordinal) && numbers[FilterFact.Quality] == 0)
            {
                var match = NumericField().Match(raw);
                if (match.Success && int.TryParse(match.Groups[2].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
                {
                    numbers[fact] = value;
                }
            }
        }

        var percentiles = new List<double>(4);
        foreach (var (range, _, fact) in types.Where(type => type.Item1 is not null))
        {
            if (!numbers.TryGetValue(fact, out var value))
            {
                return;
            }

            percentiles.Add(range!.Minimum == range.Maximum ? 100 : 100.0 * (value - range.Minimum) / (range.Maximum - range.Minimum));
        }

        numbers[FilterFact.BaseDefencePercentile] = percentiles.Count == 0 ? itemBase.Domain == "item" ? 100 : 0 : (int)percentiles.Average();
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
