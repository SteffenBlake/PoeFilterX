using System.Collections.Immutable;

namespace PoeFilterX.Engine.Syntax;

public sealed class SourceText(string path, string text)
{
    private readonly ImmutableArray<int> lineStarts = FindLineStarts(text);

    public string Path { get; } = path;

    public string Text { get; } = text;

    public ReadOnlySpan<char> Slice(SourceSpan span) => Text.AsSpan(span.Start, span.Length);

    public SourcePosition GetPosition(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Text.Length);
        var index = lineStarts.BinarySearch(offset);
        var line = index >= 0 ? index : ~index - 1;

        return new SourcePosition(line, offset - lineStarts[line]);
    }

    public SourceSpan GetLineSpan(int line)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, lineStarts.Length);
        var start = lineStarts[line];
        var end = line + 1 < lineStarts.Length ? lineStarts[line + 1] : Text.Length;
        var length = Text.AsSpan(start, end - start).TrimEnd("\r\n").Length;

        return new SourceSpan(start, length);
    }

    private static ImmutableArray<int> FindLineStarts(string text)
    {
        var starts = ImmutableArray.CreateBuilder<int>();
        starts.Add(0);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
                starts.Add(index + 1);
            }
            else if (text[index] is '\r' or '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts.ToImmutable();
    }
}
