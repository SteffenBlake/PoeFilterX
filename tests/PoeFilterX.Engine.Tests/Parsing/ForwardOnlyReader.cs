namespace PoeFilterX.Engine.Tests.Parsing;

internal sealed class ForwardOnlyReader(string text, CancellationTokenSource? cancellation = null) : TextReader
{
    private int position;

    public bool Disposed { get; private set; }

    public int ReadCalls { get; private set; }

    public override int Read()
    {
        ReadCalls++;
        if (position == 8)
        {
            cancellation?.Cancel();
        }

        return position < text.Length ? text[position++] : -1;
    }

    public override string ReadToEnd() => throw new InvalidOperationException("Must read progressively.");

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
