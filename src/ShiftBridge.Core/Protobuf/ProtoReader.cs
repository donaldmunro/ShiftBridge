namespace ShiftBridge.Core.Protobuf;

/// <summary>Un champ protobuf lu sur le fil. Seuls les types de fil utiles aux manettes Zwift sont gérés.</summary>
public readonly record struct ProtoField(int Number, int WireType, ulong Varint, ReadOnlyMemory<byte> Bytes)
{
    public const int WireVarint = 0;
    public const int WireFixed64 = 1;
    public const int WireLengthDelimited = 2;
    public const int WireFixed32 = 5;

    /// <summary>Décodage zigzag (sint32/sint64).</summary>
    public long AsZigZag() => (long)(Varint >> 1) ^ -(long)(Varint & 1);
}

/// <summary>Lecteur protobuf minimal (varint, zigzag, length-delimited), sans génération de code.</summary>
public static class ProtoReader
{
    /// <summary>
    /// Énumère les champs du message. S'arrête silencieusement à la première trame tronquée ou invalide :
    /// une notification BLE corrompue ne doit jamais lever d'exception.
    /// </summary>
    public static List<ProtoField> ReadFields(ReadOnlySpan<byte> data)
    {
        var fields = new List<ProtoField>();
        int pos = 0;
        while (pos < data.Length)
        {
            if (!TryReadVarint(data, ref pos, out ulong tag)) break;
            int number = (int)(tag >> 3);
            int wire = (int)(tag & 7);
            if (number == 0) break;

            switch (wire)
            {
                case ProtoField.WireVarint:
                    if (!TryReadVarint(data, ref pos, out ulong v)) return fields;
                    fields.Add(new ProtoField(number, wire, v, ReadOnlyMemory<byte>.Empty));
                    break;
                case ProtoField.WireLengthDelimited:
                    if (!TryReadVarint(data, ref pos, out ulong len)) return fields;
                    if (len > (ulong)(data.Length - pos)) return fields;
                    fields.Add(new ProtoField(number, wire, 0, data.Slice(pos, (int)len).ToArray()));
                    pos += (int)len;
                    break;
                case ProtoField.WireFixed32:
                    if (data.Length - pos < 4) return fields;
                    pos += 4;
                    break;
                case ProtoField.WireFixed64:
                    if (data.Length - pos < 8) return fields;
                    pos += 8;
                    break;
                default:
                    return fields; // type de fil inconnu : on abandonne
            }
        }
        return fields;
    }

    public static bool TryReadVarint(ReadOnlySpan<byte> data, ref int pos, out ulong value)
    {
        value = 0;
        int shift = 0;
        while (pos < data.Length && shift < 64)
        {
            byte b = data[pos++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
        return false;
    }
}
