namespace PoeFilterX.Engine.Parsing;

// Ownership stays with the caller; this wrapper never disposes the supplied reader.
internal sealed class CancellationTextReader(TextReader reader, CancellationToken cancellationToken) : TextReader
{
    public override int Read()
    {
        cancellationToken.ThrowIfCancellationRequested();

        return reader.Read();
    }
}
