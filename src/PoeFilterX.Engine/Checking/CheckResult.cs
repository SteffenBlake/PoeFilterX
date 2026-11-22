using System.Collections.Immutable;
using PoeFilterX.Engine.Compilation;

namespace PoeFilterX.Engine.Checking;

public sealed record CheckResult(
    int SchemaVersion,
    CheckedItem Item,
    int AreaLevel,
    CheckStatus Status,
    ImmutableArray<CheckedBlock> Blocks,
    ImmutableArray<AppliedStyle> Styles,
    StyleOperations? EffectiveStyle,
    ItemPreview? Preview
)
{
    public ItemPreview? DefaultPreview { get; init; }

    public ImmutableArray<CheckedFrame> Frames { get; init; } = [];

    public string DataVersion { get; init; } = "";
}

public sealed record CheckLocation(
    string Path,
    int Line,
    int Column
);

public sealed record CheckedCondition(
    string Name,
    string Expected,
    string? Actual,
    bool? Matches
);

public sealed record CheckedBlock(
    string Id,
    CheckLocation Location,
    ImmutableArray<CheckLocation> Trace,
    CheckStatus Status,
    ImmutableArray<CheckedCondition> Conditions
);

public sealed record AppliedStyle(
    string Id,
    string Name,
    CheckLocation Location,
    string Declaration,
    ImmutableArray<StyleContribution> Values
);

public sealed record StyleContribution(
    string Property,
    string Value,
    string? OverriddenBy
)
{
    public CheckLocation? Location { get; init; }
}

public sealed record ItemPreview(
    string Name,
    string Foreground,
    string Background,
    string Border,
    int FontSize,
    bool Hidden,
    double Opacity,
    string? Icon,
    string? Pillar
);

public sealed record CheckedItem(
    GameVersion Game,
    string Name,
    string? BaseType,
    string ItemClass,
    FilterRarity Rarity
);

public sealed record CheckedFrame(
    string Id,
    CheckLocation Location,
    ImmutableArray<FrameDeclaration> Declarations
);

public sealed record FrameDeclaration(
    string Name,
    string Value,
    string? OverriddenBy
);
