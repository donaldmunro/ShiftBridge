namespace ShiftBridge.Core.Protobuf;

/// <summary>Écriture protobuf minimale (varint, zigzag, message imbriqué), symétrique de <see cref="ProtoReader"/>.</summary>
public sealed class ProtoWriter
{
    private readonly List<byte> _bytes = [];

    public ProtoWriter Varint(int field, ulong value)
    {
        WriteVarint((ulong)(field << 3));          // type de fil 0
        WriteVarint(value);
        return this;
    }

    /// <summary>Entier signé (sint32/sint64) : codage zigzag.</summary>
    public ProtoWriter SInt(int field, long value) => Varint(field, ZigZag(value));

    public ProtoWriter Message(int field, ProtoWriter inner)
    {
        var body = inner.ToArray();
        WriteVarint((ulong)((field << 3) | 2));    // type de fil 2 : longueur + contenu
        WriteVarint((ulong)body.Length);
        _bytes.AddRange(body);
        return this;
    }

    public byte[] ToArray() => [.. _bytes];

    public static ulong ZigZag(long v) => (ulong)((v << 1) ^ (v >> 63));

    private void WriteVarint(ulong v)
    {
        while (v >= 0x80)
        {
            _bytes.Add((byte)(v | 0x80));
            v >>= 7;
        }
        _bytes.Add((byte)v);
    }
}
