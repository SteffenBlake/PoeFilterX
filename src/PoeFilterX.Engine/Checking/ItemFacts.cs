using System.Collections.Immutable;
using PoeFilterX.Engine.Compilation;

namespace PoeFilterX.Engine.Checking;

public sealed record ItemFacts(
    GameVersion Game,
    string Name,
    string? BaseType,
    string ItemClass,
    FilterRarity Rarity,
    ImmutableDictionary<FilterFact, bool> Flags,
    ImmutableDictionary<FilterFact, int> Numbers,
    ImmutableDictionary<FilterFact, ImmutableArray<string>> Texts,
    ImmutableArray<InfluenceType> Influences
)
{
    public string ModifierDomain { get; init; } = "";

    public string CopiedText { get; init; } = "";
}
