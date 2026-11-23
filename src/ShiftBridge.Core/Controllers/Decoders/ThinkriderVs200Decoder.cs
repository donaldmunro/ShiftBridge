namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Thinkrider VS200 : une notification de 5 octets par appui, jamais de relâchement.
/// <c>F3 05 03 01 FC</c> = passage au-dessus, <c>F3 05 03 00 FB</c> = passage en dessous (le dernier octet est la somme des quatre premiers).
/// </summary>
public sealed class ThinkriderVs200Decoder : IButtonDecoder
{
    public static readonly Guid Service = BleUuids.Uuid16(0xFEA0);
    public static readonly Guid Notify = BleUuids.Uuid16(0xFEA1);

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length != 5 || data[0] != 0xF3 || data[1] != 0x05 || data[2] != 0x03) return null;
        byte sum = unchecked((byte)(data[0] + data[1] + data[2] + data[3]));
        if (sum != data[4]) return null;
        return data[3] switch
        {
            0x01 => DecodedFrame.Tap("Up"),
            0x00 => DecodedFrame.Tap("Down"),
            _ => null,
        };
    }
}
