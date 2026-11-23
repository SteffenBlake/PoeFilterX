using PoeFilterX.Engine.Configuration;

namespace PoeFilterX.Engine.Requests;

public sealed record BuildRequest(
    string? Path,
    string? Output,
    IReadOnlyDictionary<string, string>? Variables = null,
    bool NoRestore = false,
    bool Locked = false,
    ProjectSettings? Settings = null
);
