using System.Collections.Immutable;
using PoeFilterX.Common;

namespace PoeFilterX.Engine.Compilation;

[OperationModel(nameof(FilterOperationsCodec))]
public sealed record FilterOperations
{
    public bool? AlternateQuality { get; init; }

    public bool? AlwaysShow { get; init; }

    public ImmutableArray<OperatorValue<ImmutableArray<string>>> ArchnemesisMod { get; init; } = [];

    public bool? AnyEnchantment { get; init; }

    public ImmutableArray<OperatorValue<int>> AreaLevel { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> BaseArmour { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> BaseDefencePercentile { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> BaseEnergyShield { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> BaseEvasion { get; init; } = [];

    public ImmutableArray<OperatorValue<ImmutableArray<string>>> BaseType { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> BaseWard { get; init; } = [];

    public bool? BlightedMap { get; init; }

    public ImmutableArray<OperatorValue<ImmutableArray<string>>> Class { get; init; } = [];

    public bool? Corrupted { get; init; }

    public ImmutableArray<OperatorValue<int>> CorruptedMods { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> DropLevel { get; init; } = [];

    public bool? ElderItem { get; init; }

    public bool? ElderMap { get; init; }

    public ImmutableArray<OperatorValue<ImmutableArray<string>>> EnchantmentPassiveNode { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> EnchantmentPassiveNum { get; init; } = [];

    public bool? Exceptional { get; init; }

    public bool? Fated { get; init; }

    public bool? Foulborn { get; init; }

    public bool? FracturedItem { get; init; }

    public ImmutableArray<OperatorValue<int>> GemLevel { get; init; } = [];

    public bool? HasCruciblePassiveTree { get; init; }

    public ImmutableArray<OperatorValue<int>> HasEaterOfWorldsImplicit { get; init; } = [];

    [Operation(Reader = "ReadModifiers", Writer = "WriteModifiers")]
    public ImmutableArray<OperatorValue<ImmutableArray<string>>> HasEnchantment { get; init; } = [];

    [Operation(Reader = "ReadModifiers", Writer = "WriteModifiers")]
    public ImmutableArray<OperatorValue<ImmutableArray<string>>> HasExplicitMod { get; init; } = [];

    public bool? HasImplicitMod { get; init; }

    public ImmutableArray<InfluenceType> HasInfluence { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> HasSearingExarchImplicit { get; init; } = [];

    public bool? HasVaalUniqueMod { get; init; }

    public ImmutableArray<OperatorValue<int>> Height { get; init; } = [];

    public bool? Identified { get; init; }

    public bool? Imbued { get; init; }

    public bool? IsVaalUnique { get; init; }

    public ImmutableArray<OperatorValue<int>> ItemLevel { get; init; } = [];

    [Operation(Maximum = 6)]
    public ImmutableArray<OperatorValue<int>> LinkedSockets { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> MapTier { get; init; } = [];

    [Operation(Maximum = 100)]
    public ImmutableArray<OperatorValue<int>> MemoryStrands { get; init; } = [];

    public bool? MirageMap { get; init; }

    public bool? Mirrored { get; init; }

    public ImmutableArray<OperatorValue<int>> Quality { get; init; } = [];

    public ImmutableArray<OperatorValue<FilterRarity>> Rarity { get; init; } = [];

    public bool? Replica { get; init; }

    public bool? Scourged { get; init; }

    public bool? ShapedMap { get; init; }

    public bool? ShaperItem { get; init; }

    public ImmutableArray<OperatorValue<ImmutableArray<string>>> SocketGroup { get; init; } = [];

    public ImmutableArray<OperatorValue<ImmutableArray<string>>> Sockets { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> StackSize { get; init; } = [];

    public bool? SynthesisedItem { get; init; }

    [Operation(Reader = "ReadTransfiguredGem", Writer = "WriteTransfiguredGem")]
    public TransfiguredGemValue? TransfiguredGem { get; init; }

    public bool? TwiceCorrupted { get; init; }

    public bool? UberBlightedMap { get; init; }

    public ImmutableArray<OperatorValue<int>> UnidentifiedItemTier { get; init; } = [];

    public bool? Vestigial { get; init; }

    public ImmutableArray<OperatorValue<int>> WaystoneTier { get; init; } = [];

    public ImmutableArray<OperatorValue<int>> Width { get; init; } = [];

    public bool? ZanaMemory { get; init; }

    [Operation(SkipOutput = true)]
    public ImmutableArray<string> Style { get; init; } = [];

    [Operation(SkipOutput = true, Reader = "ReadHasValue")]
    public ImmutableArray<string> HasValue { get; init; } = [];

    [Operation(SkipOutput = true, Reader = "ReadIsTrue")]
    public ImmutableArray<bool> IsTrue { get; init; } = [];

}
