using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using PoeFilterX.Engine.Compilation;

namespace PoeFilterX.Engine.Checking;

internal sealed partial class ItemModifierCatalog
{
    private static readonly Lazy<ItemModifierCatalog> Poe1 = new(() => new(GameData.Poe1ItemModifiers()));
    private static readonly Lazy<ItemModifierCatalog> Poe2 = new(() => new(GameData.Poe2ItemModifiers()));
    private readonly FrozenDictionary<string, ImmutableArray<ItemModifier>> signatures;

    private ItemModifierCatalog(ItemModifier[] modifiers)
    {
        signatures = modifiers.Where(modifier => !modifier.Stats.IsEmpty)
            .SelectMany(modifier => modifier.Stats.Select(stat => (stat.Signature, Modifier: modifier)))
            .GroupBy(item => item.Signature, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.Select(item => item.Modifier).Distinct().ToImmutableArray());
    }

    internal static ItemModifierCatalog For(GameVersion game) => game == GameVersion.Poe1 ? Poe1.Value : Poe2.Value;

    internal ImmutableArray<string>? Resolve(ItemFacts item, bool enchantments)
    {
        var lines = item.CopiedText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var displayed = DisplayedStats(lines, enchantments).Select(NormalizeDisplay).ToArray();
        var stats = displayed.Select(line => (Signature: Signature(line), Values: Values(line)))
            .ToLookup(item => item.Signature, StringComparer.Ordinal);
        var names = ImmutableArray.CreateBuilder<string>();
        foreach (var group in stats)
        {
            if (!signatures.TryGetValue(group.Key, out var candidates))
            {
                continue;
            }

            var matching = candidates.Where(candidate => candidate.Domain == item.ModifierDomain
                && MatchesLabel(candidate, item, enchantments)
                && (candidate.Kind == ItemModifierKind.Enchantment) == enchantments
                && candidate.Stats.All(stat => stats[stat.Signature].Any(actual => Fits(stat.Ranges, actual.Values))))
                .Select(candidate => enchantments ? candidate.Id : candidate.Name)
                .Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
            if (matching.Length > 1)
            {
                return null;
            }

            names.AddRange(matching);
        }

        return names.Distinct(StringComparer.Ordinal).ToImmutableArray();
    }

    private static bool MatchesLabel(
        ItemModifier modifier,
        ItemFacts item,
        bool enchantments
    )
    {
        if (enchantments || item.Rarity != FilterRarity.Magic)
        {
            return true;
        }

        var label = item.Name.StartsWith("Superior ", StringComparison.Ordinal) ? item.Name[9..] : item.Name;

        var prefix = label.StartsWith(modifier.Name + " ", StringComparison.Ordinal);
        var suffix = label.EndsWith(" " + modifier.Name, StringComparison.Ordinal);

        return modifier.Kind switch
        {
            ItemModifierKind.Prefix => prefix,
            ItemModifierKind.Suffix => suffix,
            _ => prefix || suffix
        };
    }

    private static IEnumerable<string> DisplayedStats(string[] lines, bool enchantments)
    {
        bool? enchantmentSection = null;
        var excludedSection = false;
        foreach (var line in lines)
        {
            if (line == "--------")
            {
                enchantmentSection = null;
                excludedSection = false;
                continue;
            }

            if (line.StartsWith('{'))
            {
                enchantmentSection = line.Contains("Enchantment Modifier", StringComparison.Ordinal)
                    || line.Contains("Corruption Enhancement", StringComparison.Ordinal);
                excludedSection = !enchantmentSection.Value && !line.Contains("Prefix Modifier", StringComparison.Ordinal)
                    && !line.Contains("Suffix Modifier", StringComparison.Ordinal);
                continue;
            }

            if (excludedSection || line.EndsWith("(implicit)", StringComparison.Ordinal) || line.EndsWith("(rune)", StringComparison.Ordinal))
            {
                continue;
            }

            var isEnchantment = enchantmentSection ?? line.EndsWith("(enchant)", StringComparison.Ordinal);
            if (isEnchantment == enchantments)
            {
                yield return line;
            }
        }
    }

    private static bool Fits(
        ImmutableArray<ModifierRange> ranges,
        ImmutableArray<double> values
    )
    {
        if (ranges.Length != values.Length)
        {
            return false;
        }

        for (var index = 0; index < ranges.Length; index++)
        {
            if (values[index] < ranges[index].Minimum || values[index] > ranges[index].Maximum)
            {
                return false;
            }
        }

        return true;
    }

    private static string Signature(string line)
    {
        return Numbers().Replace(line, "#").Replace("+#", "#", StringComparison.Ordinal).Replace("-#", "#", StringComparison.Ordinal);
    }

    private static ImmutableArray<double> Values(string line)
    {
        return [.. Numbers().Matches(line).Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))];
    }

    private static string NormalizeDisplay(string line)
    {
        line = RollAnnotations().Replace(line, "");
        line = ModifierAnnotations().Replace(line, "");
        var unscalable = line.IndexOf(" — Unscalable Value", StringComparison.Ordinal);

        return Links().Replace(unscalable < 0 ? line : line[..unscalable], LinkText).Trim();
    }

    private static string LinkText(Match match)
    {
        var text = match.Groups[1].Value;
        var separator = text.IndexOf('|');

        return separator < 0 ? text : text[(separator + 1)..];
    }

    [GeneratedRegex(@"\[([^\[\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex Links();

    [GeneratedRegex(@"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex Numbers();

    [GeneratedRegex(@"(?<=\d)\([-+]?\d+(?:\.\d+)?-[-+]?\d+(?:\.\d+)?\)", RegexOptions.CultureInvariant)]
    private static partial Regex RollAnnotations();

    [GeneratedRegex(@"\s*\((?:crafted|fractured|enchant|desecrated|scourge)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ModifierAnnotations();
}
