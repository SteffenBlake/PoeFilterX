namespace PoeFilterX.Engine.Tests.Parsing;

internal sealed class ChunkedReadStream(byte[] bytes) : MemoryStream(bytes)
{
    public override int Read(
        byte[] buffer,
        int offset,
        int count
    )
    {
        return base.Read(buffer, offset, Math.Min(count, 1));
    }

    public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
}
