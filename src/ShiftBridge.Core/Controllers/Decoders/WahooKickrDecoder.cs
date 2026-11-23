namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Wahoo KICKR Bike / Bike Shift : trames de 3 octets <c>PP QQ RR</c>. <c>PPQQ</c> identifie la touche (table ci-dessous) ;
/// le bit 7 de <c>RR</c> = enfoncée, effacé = relâchée (les 7 bits bas sont un compteur roulant, ignoré). Les trames d'une
/// autre longueur (données du trainer) et les touches inconnues sont ignorées.
/// </summary>
public sealed class WahooKickrDecoder : IButtonDecoder
{
    public static readonly Guid Service = new("a026ee0d-0a7d-4ab3-97fa-f1500f9feb8b");
    public static readonly Guid Notify = new("a026e03c-0a7d-4ab3-97fa-f1500f9feb8b");

    private static readonly Dictionary<ushort, string> Table = new()
    {
        [0x0001] = "RightUp", [0x8000] = "RightDown", [0x0008] = "RightSteer",
        [0x0200] = "LeftUp", [0x0400] = "LeftDown", [0x2000] = "LeftSteer",
        [0x0004] = "ShiftUpRight", [0x0002] = "ShiftDownRight", [0x1000] = "ShiftUpLeft", [0x0800] = "ShiftDownLeft",
        [0x4000] = "RightBrake", [0x0100] = "LeftBrake",
    };

    private readonly HashSet<string> _held = [];

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length != 3) return null;
        ushort key = (ushort)((data[0] << 8) | data[1]);
        bool pressed = (data[2] & 0x80) != 0;
        if (key == 0) { _held.Clear(); return new DecodedFrame(new HashSet<string>(), DecodedFrame.NoTaps); }
        if (!Table.TryGetValue(key, out var name)) return null;
        if (pressed) _held.Add(name); else _held.Remove(name);
        return new DecodedFrame(new HashSet<string>(_held), DecodedFrame.NoTaps);
    }
}
