namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// CYCPLUS T7H (leviers de vélo connecté, Nordic UART) : trame de 4 octets <c>AA b1 touches somme</c>, avec
/// <c>somme = (AA + b1 + touches) &amp; FF</c>. Le bit 0x80 de « touches » = levier droit. Bits : 0x01 vitesse +, 0x02 vitesse −,
/// 0x08 flèche arrière, 0x10 flèche avant (les autres sont ignorés). Chaque levier est un appareil BLE distinct.
/// </summary>
public sealed class CycplusT7hDecoder : IButtonDecoder
{
    private static readonly (int Mask, string Name)[] Bits = [(0x01, "ShiftUp"), (0x02, "ShiftDown"), (0x08, "RearArrow"), (0x10, "FrontArrow")];
    private readonly HashSet<string> _left = [], _right = [];

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length != 4 || data[0] != 0xAA) return null;
        if (((data[0] + data[1] + data[2]) & 0xFF) != data[3]) return null;
        bool right = (data[2] & 0x80) != 0;
        var side = right ? _right : _left;
        side.Clear();
        foreach (var (mask, name) in Bits) if ((data[2] & mask) != 0) side.Add((right ? "R." : "L.") + name);
        var held = new HashSet<string>(_left);
        held.UnionWith(_right);
        return new DecodedFrame(held, DecodedFrame.NoTaps);
    }
}
