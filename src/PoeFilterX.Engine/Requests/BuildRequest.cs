namespace PoeFilterX.Engine.Requests;

public sealed record BuildRequest(
    string? Path,
    string? Output,
    bool Verbose
);
