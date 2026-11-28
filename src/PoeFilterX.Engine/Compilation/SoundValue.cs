namespace PoeFilterX.Engine.Compilation;

public sealed record SoundValue(
    int? Id = null,
    int? Volume = null,
    bool? Positional = null,
    bool? Enabled = null,
    string? Path = null,
    bool? CustomEnabled = null,
    bool UseBuiltIn = false,
    bool? Optional = null,
    int? CustomVolume = null
);
