using System.Buffers.Binary;

namespace LibWorld.TexturePack;

[Container(0x33310000)]
public record PackMetadata
{
    public PackInfo? Info;
    public TextureHashes? Hashes;
    public CompressedTextureEntries? CompressedEntries;
}

[Chunk(0x33310001)]
public record PackInfo : ITLVChunk
{
    // TPK version
    // 9 - NFS: World
    // 8 - NFS: Carbon
    public uint Version;

    // Internal name for the TPK
    public string Name = String.Empty;

    // XML file the TPK was originally created from
    public string SourceFile = String.Empty;

    // Texture pack hash. Equal to binhash(Name)
    public uint Hash;

    public override void FromByteSpan(ReadOnlySpan<byte> span) {
        // expected length = 124
        Version = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
        Name = BinaryUtil.ReadString(span.Slice(4, 24));
        SourceFile = BinaryUtil.ReadString(span.Slice(32, 64));
        Hash = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(96, 4));
    }
}

[Chunk(0x33310002)]
public record TextureHashes : ITLVChunk
{
    // List of texture hashes
    public HashSet<uint> Hashes = new();

    public override void FromByteSpan(ReadOnlySpan<byte> span) {
        Hashes = new();
        for (int i = 0; i < span.Length; i += 8) {
            Hashes.Add(BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i, 4)));
        }
    }
}

[Chunk(0x33310003)]
public record CompressedTextureEntries : ITLVChunk
{
    public HashSet<CompressedTextureEntry> Entries = new();

    public override void FromByteSpan(ReadOnlySpan<byte> span) {
        Entries = new();
        for (int i = 0; i < span.Length; i += 36) {
            var entry = new CompressedTextureEntry();
            entry.FromByteSpan(span.Slice(i, 36));
            Entries.Add(entry);
        }
    }
}

public record CompressedTextureEntry : ITLVChunk
{
    public uint Hash { get; set; }
    public uint Offset { get; set; }
    public uint CompressedSize { get; set; }
    public uint UncompressedSize { get; set; }
    public uint InfoSize { get; set; }
    public uint UnknownSize { get; set; }
}