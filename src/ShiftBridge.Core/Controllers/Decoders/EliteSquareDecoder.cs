namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Elite Square (cadre connecté) : les octets 3 à 6 forment un mot de 32 bits, un bit par touche enfoncée (0 = tout relâché).
/// Les sources divergent sur la caractéristique (…0043 ou …0045) : le contrôleur s'abonne à celle qui existe.
/// </summary>
public sealed class EliteSquareDecoder : IButtonDecoder
{
    public static readonly Guid Service = new("347b0001-7635-408b-8918-8ff3949ce592");
    public static readonly Guid Notify43 = new("347b0043-7635-408b-8918-8ff3949ce592");
    public static readonly Guid Notify45 = new("347b0045-7635-408b-8918-8ff3949ce592");

    private static readonly (uint Mask, string Name)[] Bits =
    [
        (0x00000200, "Up"), (0x00000800, "Down"), (0x00000100, "Left"), (0x00000400, "Right"),
        (0x00002000, "X"), (0x00001000, "Square"),
        (0x00000002, "LeftShift1"), (0x00000001, "LeftShift2"), (0x00004000, "LeftBrake"), (0x00008000, "CampyLeft"),
        (0x02000000, "Y"), (0x01000000, "A"), (0x08000000, "B"), (0x04000000, "Z"), (0x20000000, "Circle"), (0x10000000, "Triangle"),
        (0x00020000, "RightShift1"), (0x00010000, "RightShift2"), (0x40000000, "RightBrake"), (0x80000000, "CampyRight"),
    ];

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length < 7) return null;
        uint key = ((uint)data[3] << 24) | ((uint)data[4] << 16) | ((uint)data[5] << 8) | data[6];
        var held = new HashSet<string>();
        foreach (var (mask, name) in Bits) if ((key & mask) != 0) held.Add(name);
        return new DecodedFrame(held, DecodedFrame.NoTaps);
    }
}
