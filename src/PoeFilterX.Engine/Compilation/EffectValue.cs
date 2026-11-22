namespace PoeFilterX.Engine.Compilation;

public sealed record EffectValue(
    FilterColor? Color = null,
    bool? Temporary = null,
    bool? Enabled = null
);
