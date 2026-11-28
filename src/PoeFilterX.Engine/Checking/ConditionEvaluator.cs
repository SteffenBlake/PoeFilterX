using System.Collections.Immutable;
using System.Globalization;
using PoeFilterX.Engine.Compilation;

namespace PoeFilterX.Engine.Checking;

public sealed class ConditionEvaluator(
    ItemFacts item,
    int areaLevel,
    Dictionary<FilterFact, ImmutableArray<string>?>? resolvedFacts = null
)
{
    private readonly List<CheckedCondition> conditions = [];

    internal ImmutableArray<CheckedCondition> Conditions => [.. conditions];

    public void Add(FilterFact fact, bool? expected)
    {
        if (expected.HasValue)
        {
            var actual = item.Flags.TryGetValue(fact, out var value) ? value : (bool?)null;
            conditions.Add(new(fact.ToString(), expected.Value.ToString(), actual?.ToString(), actual.HasValue ? actual == expected : null));
        }
    }

    public void Add(FilterFact fact, ImmutableArray<OperatorValue<int>> expected)
    {
        foreach (var condition in expected)
        {
            int? actual = fact == FilterFact.AreaLevel ? areaLevel
                : item.Numbers.TryGetValue(fact, out var value) ? value : null;
            conditions.Add(new(fact.ToString(), $"{condition.Operator.ToSymbol()} {condition.Value}",
                actual?.ToString(CultureInfo.InvariantCulture), actual.HasValue ? Compare(actual.Value, condition.Value, condition.Operator) : null));
        }
    }

    public void Add(FilterFact fact, ImmutableArray<OperatorValue<FilterRarity>> expected)
    {
        foreach (var condition in expected)
        {
            conditions.Add(new(fact.ToString(), $"{condition.Operator.ToSymbol()} {condition.Value}",
                item.Rarity.ToString(), Compare((int)item.Rarity, (int)condition.Value, condition.Operator)));
        }
    }

    public void Add(FilterFact fact, ImmutableArray<InfluenceType> expected)
    {
        if (!expected.IsEmpty)
        {
            var matches = expected.Any(influence => influence == InfluenceType.None
                ? item.Influences.IsEmpty : item.Influences.Contains(influence));
            conditions.Add(new(fact.ToString(), string.Join(' ', expected),
                item.Influences.IsEmpty ? "None" : string.Join(' ', item.Influences), matches));
        }
    }

    public void Add(FilterFact fact, ImmutableArray<OperatorValue<ImmutableArray<string>>> expected)
    {
        if (fact is FilterFact.HasExplicitMod or FilterFact.HasEnchantment)
        {
            foreach (var condition in expected.Where(IsCounted))
            {
                var known = TryGetText(fact, out var actual);
                var matches = known ? MatchText(fact, actual, [.. condition.Value], condition.Operator) : (bool?)null;
                conditions.Add(new(fact.ToString(), $"{condition.Operator.ToSymbol()} {string.Join(' ', condition.Value)}",
                    known ? string.Join(' ', actual) : null, matches));
            }

            expected = [.. expected.Where(condition => !IsCounted(condition))];
        }

        // The writer joins declarations with the same operator into one alternative list.
        foreach (var group in expected.GroupBy(condition => condition.Operator))
        {
            var alternatives = group.SelectMany(condition => condition.Value).ToArray();
            var known = TryGetText(fact, out var actual);
            var matches = known ? MatchText(fact, actual, alternatives, group.Key) : (bool?)null;
            conditions.Add(new(fact.ToString(), $"{group.Key.ToSymbol()} {string.Join(' ', alternatives)}",
                known ? string.Join(' ', actual) : null, matches));
        }
    }

    public void Add(FilterFact fact, TransfiguredGemValue? expected)
    {
        if (expected?.Any is { } boolean)
        {
            Add(fact, boolean);
        }
        else if (expected is not null)
        {
            Add(fact, expected.Names);
        }
    }

    private static bool IsCounted(OperatorValue<ImmutableArray<string>> condition)
    {
        return condition.Value.Length > 0 && int.TryParse(condition.Value[0], out _);
    }

    private bool TryGetText(FilterFact fact, out ImmutableArray<string> actual)
    {
        if (item.Texts.TryGetValue(fact, out actual))
        {
            return true;
        }

        if (fact is not (FilterFact.HasExplicitMod or FilterFact.HasEnchantment)
            || (fact == FilterFact.HasExplicitMod && item.Rarity == FilterRarity.Unique))
        {
            return false;
        }

        if (resolvedFacts is null || !resolvedFacts.TryGetValue(fact, out var resolved))
        {
            resolved = ItemModifierCatalog.For(item.Game).Resolve(item, fact == FilterFact.HasEnchantment);
            resolvedFacts?.Add(fact, resolved);
        }

        actual = resolved ?? [];

        return resolved.HasValue;
    }

    private static bool MatchText(
        FilterFact fact,
        ImmutableArray<string> actual,
        string[] expected,
        ComparisonOperator comparison
    )
    {
        if (fact is FilterFact.Sockets or FilterFact.SocketGroup)
        {
            var match = actual.Any(sockets => expected.Any(pattern => SocketMatches(sockets, pattern, comparison)));
            return comparison == ComparisonOperator.NotEqual ? !match : match;
        }

        if (fact is FilterFact.HasExplicitMod or FilterFact.HasEnchantment && expected.Length > 0
            && int.TryParse(expected[0], out var count))
        {
            var matched = actual.Count(value => expected.Skip(1).Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
            return Compare(matched, count, comparison);
        }

        var found = actual.Any(value => expected.Any(pattern => comparison == ComparisonOperator.ExactEqual
            ? value.Equals(pattern, StringComparison.OrdinalIgnoreCase) : value.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

        return comparison == ComparisonOperator.NotEqual ? !found : found;
    }

    private static bool SocketMatches(
        string actual,
        string pattern,
        ComparisonOperator comparison
    )
    {
        var digits = 0;
        while (digits < pattern.Length && char.IsAsciiDigit(pattern[digits]))
        {
            digits++;
        }

        var total = actual.Count(char.IsLetter);
        var op = comparison == ComparisonOperator.NotEqual ? ComparisonOperator.Equal : comparison;
        if (digits > 0 && (!int.TryParse(pattern.AsSpan(0, digits), out var count) || !Compare(total, count, op)))
        {
            return false;
        }

        foreach (var color in pattern[digits..].Distinct())
        {
            var requested = pattern[digits..].Count(value => value == color);
            var available = actual.Count(value => value == color);
            if (!Compare(available, requested, op == ComparisonOperator.ExactEqual ? op : ComparisonOperator.GreaterThanOrEqual))
            {
                return false;
            }
        }

        return digits > 0 || op != ComparisonOperator.ExactEqual || total == pattern.Length;
    }

    private static bool Compare(
        int actual,
        int expected,
        ComparisonOperator comparison
    )
    {
        return comparison switch
        {
            ComparisonOperator.Equal or ComparisonOperator.ExactEqual => actual == expected,
            ComparisonOperator.NotEqual => actual != expected,
            ComparisonOperator.GreaterThan => actual > expected,
            ComparisonOperator.GreaterThanOrEqual => actual >= expected,
            ComparisonOperator.LessThan => actual < expected,
            ComparisonOperator.LessThanOrEqual => actual <= expected,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison))
        };
    }
}
