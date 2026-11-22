namespace PoeFilterX.Engine.Compilation;

public sealed record IconValue(
    int? Size = null,
    FilterColor? Color = null,
    MiniMapIconShape? Shape = null,
    bool? Enabled = null
);
