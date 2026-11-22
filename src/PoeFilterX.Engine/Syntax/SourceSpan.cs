namespace PoeFilterX.Engine.Syntax;

/// <summary>A half-open range measured in UTF-16 code units, matching .NET strings.</summary>
public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;
}
