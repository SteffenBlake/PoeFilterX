namespace PoeFilterX.Engine.Syntax;

/// <summary>A zero-based line and UTF-16 column.</summary>
public readonly record struct SourcePosition(int Line, int Character);
