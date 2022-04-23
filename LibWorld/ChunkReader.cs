namespace LibWorld;

using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using LibWorld.IO;

public interface IGetChunks {
    public IEnumerable<BaseChunk> GetChunks();
}

public class ChunkReader : IGetChunks {
    private DataReader reader;
    private long nextChunkPos;

    public ChunkReader(Stream rd) {
        this.reader = new StreamDataReader(rd);
    }

    public ChunkReader(DataReader rd) {
        this.reader = rd;
    }

    public IEnumerable<BaseChunk> GetChunks() {
        while (true) {
            BaseChunk chunk;
            try {
                chunk = ReadNextChunk();
            } catch (EndOfStreamException) {
                yield break;
            }
            yield return chunk;
        }
    }

    public BaseChunk ReadNextChunk() {
        var chunkHeader = new byte[8].AsSpan();
        reader.ReadAt(nextChunkPos, chunkHeader);

        var type = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.Slice(0, 4));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.Slice(4, 4));

        var dataStart = nextChunkPos + 8;
        nextChunkPos += (long)8 + length;

        if (type >> 31 == 1) {
            return new TLVContainer{
                TypeID = type ^ ((uint)1 << 31),
                Data = reader.Slice(dataStart, length),
            };
        } else {
            return new TLVChunk{
                TypeID = type,
                Data = reader.Slice(dataStart, length),
            };
        }
    }
}

public abstract class BaseChunk {
    // Type ID of the chunk
    public uint TypeID;

    // Chunk data
    public DataReader Data;
}

public class TLVChunk : BaseChunk {
    
}

public class TLVContainer : BaseChunk, IGetChunks {
    public IEnumerable<BaseChunk> GetChunks() {
        var rd = new ChunkReader(Data);
        return rd.GetChunks();
    }
}

public abstract record ITLVChunk {
    public virtual void FromByteSpan(ReadOnlySpan<byte> span) {
        var offset = 0;
        var type = GetType();
        foreach (var prop in type.GetProperties()) {
            if (prop.PropertyType == typeof(uint)) {
                prop.SetValue(this, BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)));
                offset += 4;
            } else {
                throw new InvalidOperationException("Can't deserialize property");
            }
        }
    }
}

public static class BinaryUtil {
    public static String ReadString(ReadOnlySpan<byte> span) {
        int nullIndex = span.IndexOf((byte)0);
        if (nullIndex == -1) nullIndex = span.Length;
        return new UTF8Encoding(false, true).GetString(span.Slice(0, nullIndex));
    }

    public static void ReadBytes(Stream rd, Span<byte> span) {
        int totalRead = 0;
        do {
            int bytesRead = rd.Read(span.Slice(totalRead));
            if (bytesRead == 0) { // EOF
                throw new EndOfStreamException();
            }
            totalRead += bytesRead;
        } while (totalRead < span.Length);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ChunkAttribute : Attribute {
    public uint id;
    public ChunkAttribute(uint id) {
        this.id = id;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ContainerAttribute : Attribute {
    public uint id;
    public ContainerAttribute(uint id) {
        this.id = id;
    }
}

public class ChunkDeserializer {
    public static T DeserializeContainer<T>(IGetChunks gc) {
        var type = typeof(T);
        object? value = Activator.CreateInstance(type);
        var fields = new Dictionary<uint, FieldInfo>();
        foreach (var field in type.GetFields()) {
            var ft = field.FieldType;
            if (!typeof(ITLVChunk).IsAssignableFrom(ft)) {
                continue;
            }
            try {
                var attr = (ChunkAttribute) ft.GetCustomAttributes(false).First(f => f is ChunkAttribute);
                fields[attr.id] = field;
            } catch {}
        }

        foreach (var chunk in gc.GetChunks()) {
            FieldInfo? field;
            if (fields.TryGetValue(chunk.TypeID, out field)) {
                var ch = DeserializeChunk(field.FieldType, (TLVChunk) chunk);
                field.SetValue(value, ch);
            }
        }
        return (T) value!;
    }

    public static T DeserializeChunk<T>(TLVChunk chunk) where T: ITLVChunk {
        var value = Activator.CreateInstance<T>();
        value.FromByteSpan(((BoundedDataReader)chunk.Data).AsSpan());
        return value;
    }

    private static object DeserializeChunk(Type type, TLVChunk chunk) {
        var value = Activator.CreateInstance(type);
        ((ITLVChunk)value!).FromByteSpan(((BoundedDataReader)chunk.Data).AsSpan());
        return value;
    }
}
