using System.Collections.Immutable;

namespace PoeFilterX.Engine.Checking;

internal sealed record ItemModifier(
    string Id,
    string Name,
    ItemModifierKind Kind,
    string Domain,
    ImmutableArray<ModifierStat> Stats
);

internal enum ItemModifierKind { Prefix, Suffix, Enchantment, Unique }

internal sealed record ModifierStat(string Signature, ImmutableArray<ModifierRange> Ranges);

internal readonly record struct ModifierRange(double Minimum, double Maximum);
