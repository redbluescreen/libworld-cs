namespace LibWorld.IO;

/// <summary>
/// DataReader represents an interface for reading data blocks from arbitrary offsets
/// </summary>
public abstract class DataReader
{
    public virtual long FileOffset { get; }

    public abstract void ReadAt(long offset, Span<byte> span);

    public virtual DataReader Slice(long offset, long length)
    {
        return new BoundedDataReader(this, offset, length);
    }
}

/// <summary>
/// StreamDataReader represents a DataReader backed by a Stream
/// </summary>
public class StreamDataReader : DataReader
{
    private Stream stream;
    private long startOffset;

    public override long FileOffset
    {
        get { return startOffset; }
    }

    public StreamDataReader(Stream stream)
    {
        this.stream = stream;
        this.startOffset = stream.Position;
    }

    public override void ReadAt(long offset, Span<byte> span)
    {
        stream.Position = startOffset + offset;
        BinaryUtil.ReadBytes(stream, span);
    }
}

/// <summary>
/// BoundedDataReader wraps a DataReader and limits the reads to a specified span (offset and length)
/// </summary>
public class BoundedDataReader : DataReader
{
    private DataReader reader;
    private long baseOffset;
    private long length;

    public override long FileOffset
    {
        get { return reader.FileOffset + baseOffset; }
    }

    public BoundedDataReader(DataReader reader, long baseOffset, long length)
    {
        this.reader = reader;
        this.baseOffset = baseOffset;
        this.length = length;
    }

    public override void ReadAt(long offset, Span<byte> span)
    {
        if (offset > length || span.Length > length - offset)
        {
            throw new EndOfStreamException();
        }

        reader.ReadAt(baseOffset + offset, span);
    }

    public override DataReader Slice(long offset, long length)
    {
        if (offset > this.length || length > this.length - offset)
        {
            throw new ArgumentOutOfRangeException();
        }

        return new BoundedDataReader(this.reader, baseOffset + offset, length);
    }

    public ReadOnlySpan<byte> AsSpan()
    {
        var buf = new byte[length];
        ReadAt(0, buf);
        return buf;
    }
}