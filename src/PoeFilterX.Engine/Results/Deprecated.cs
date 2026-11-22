namespace PoeFilterX.Engine.Results;

public sealed record Deprecated<T>(T Value, DateTime DeprecatedAfterUtc, string? Message = null);
