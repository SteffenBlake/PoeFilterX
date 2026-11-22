namespace PoeFilterX.Engine.Packages;

public sealed record RestoreOptions(
    bool Update = false,
    bool Locked = false,
    string? CacheRoot = null,
    bool NoRestore = false
);
