using System.Collections.Immutable;
using System.Globalization;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Checking;

public static class ItemFactOverrides
{
    public static Result<ItemFacts> Apply(ItemFacts item, IReadOnlyDictionary<string, string>? overrides)
    {
        foreach (var (name, text) in overrides ?? ImmutableDictionary<string, string>.Empty)
        {
            if (!Enum.TryParse<FilterFact>(name, true, out var fact) || !Enum.IsDefined(fact))
            {
                return new ParseError(DiagnosticCodes.InvalidItem, $"Unrecognized item fact '{name}'.");
            }

            if (fact == FilterFact.AreaLevel)
            {
                return new ParseError(DiagnosticCodes.InvalidItem, "Use the area-level selector or --area-level to set AreaLevel.");
            }

            var parsed = FilterOperationsCodec.Override(item, fact, text);
            if (parsed is ParseError error)
            {
                return error;
            }

            item = (ItemFacts)parsed.Value!;
        }

        return item;
    }

    public static Result<ItemFacts> Set(
        ItemFacts item,
        FilterFact fact,
        string text,
        TransfiguredGemValue? _
    )
    {
        return Set(item, fact, text, default(ImmutableArray<OperatorValue<ImmutableArray<string>>>));
    }

    public static Result<ItemFacts> Set(
        ItemFacts item,
        FilterFact fact,
        string text,
        bool? _
    )
    {
        return bool.TryParse(text, out var value) ? item with { Flags = item.Flags.SetItem(fact, value) }
            : new ParseError(DiagnosticCodes.InvalidItem, $"{fact} requires true or false.");
    }

    public static Result<ItemFacts> Set(
        ItemFacts item,
        FilterFact fact,
        string text,
        ImmutableArray<OperatorValue<int>> _
    )
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? item with { Numbers = item.Numbers.SetItem(fact, value) }
            : new ParseError(DiagnosticCodes.InvalidItem, $"{fact} requires a nonnegative integer.");
    }

    public static Result<ItemFacts> Set(
        ItemFacts item,
        FilterFact fact,
        string text,
        ImmutableArray<OperatorValue<ImmutableArray<string>>> _
    )
    {
        if (fact == FilterFact.TransfiguredGem && bool.TryParse(text, out var transfigured))
        {
            return item with { Flags = item.Flags.SetItem(fact, transfigured) };
        }

        var values = text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fact is FilterFact.BaseType or FilterFact.Class && values.Length != 1)
        {
            return new ParseError(DiagnosticCodes.InvalidItem, $"{fact} requires one item identity value.");
        }

        return item with
        {
            Texts = item.Texts.SetItem(fact, [.. values]),
            Flags = fact == FilterFact.TransfiguredGem ? item.Flags.SetItem(fact, values.Length > 0) : item.Flags,
            BaseType = fact == FilterFact.BaseType ? values[0] : item.BaseType,
            ItemClass = fact == FilterFact.Class ? values[0] : item.ItemClass
        };
    }

    public static Result<ItemFacts> Set(
        ItemFacts item,
        FilterFact fact,
        string text,
        ImmutableArray<OperatorValue<FilterRarity>> _
    )
    {
        return Enum.TryParse<FilterRarity>(text, true, out var value) && Enum.IsDefined(value)
            ? item with { Rarity = value } : new ParseError(DiagnosticCodes.InvalidItem, $"{fact} requires a valid item rarity.");
    }

    public static Result<ItemFacts> Set(
        ItemFacts item,
        FilterFact fact,
        string text,
        ImmutableArray<InfluenceType> _
    )
    {
        var values = ImmutableArray.CreateBuilder<InfluenceType>();
        foreach (var name in text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<InfluenceType>(name, true, out var value) || !Enum.IsDefined(value))
            {
                return new ParseError(DiagnosticCodes.InvalidItem, $"Invalid {fact} value '{name}'.");
            }

            if (value != InfluenceType.None)
            {
                values.Add(value);
            }
        }

        return item with { Influences = values.ToImmutable() };
    }
}
