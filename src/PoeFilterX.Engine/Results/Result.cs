namespace PoeFilterX.Engine.Results;

public readonly union Result<T>(T, Deprecated<T>, ParseError);
