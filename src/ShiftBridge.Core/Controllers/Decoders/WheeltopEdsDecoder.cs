namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Wheeltop EDS (Nordic UART). Deux formats : OX (3 octets) <c>04 code xor</c> avec <c>xor = 04 ^ code</c>, et TX (4 octets)
/// <c>04 type code somme</c> avec <c>somme = (04 + type + code) &amp; FF</c>, type 0x36 gauche, 0x38 droite, 0x39 avant.
/// Codes : 0x02 haut, 0x01 bas, 0x06 haut fin, 0x05 bas fin (appuis) ; 0x0A / 0x09 relâchements ; 0x03/0x04/0x07/0x08 maintiens
/// (ignorés) ; 0x10 état 1 Hz (ignoré).
/// </summary>
public sealed class WheeltopEdsDecoder : IButtonDecoder
{
    /// <summary>Préfixe constant des données constructeur d'une annonce Wheeltop (company id petit-boutiste + charge utile).</summary>
    public static readonly byte[] AdvertPrefix = [0x07, 0x0F, 0x00, 0x14, 0x55, 0x6A, 0x84];

    private readonly Dictionary<string, HashSet<string>> _heldByUnit = [];

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        string unit; byte code;
        if (data.Length == 3 && data[0] == 0x04 && data[2] == (0x04 ^ data[1])) { unit = ""; code = data[1]; }
        else if (data.Length == 4 && data[0] == 0x04 && (data[1] is 0x36 or 0x38 or 0x39) && data[3] == ((0x04 + data[1] + data[2]) & 0xFF))
        {
            unit = data[1] switch { 0x36 => "L.", 0x38 => "R.", _ => "F." };
            code = data[2];
        }
        else return null;

        if (!_heldByUnit.TryGetValue(unit, out var held)) _heldByUnit[unit] = held = [];
        switch (code)
        {
            case 0x02: held.Add(unit + "Up"); break;
            case 0x01: held.Add(unit + "Down"); break;
            case 0x06: held.Add(unit + "FineUp"); break;
            case 0x05: held.Add(unit + "FineDown"); break;
            case 0x0A: held.Remove(unit + "Up"); held.Remove(unit + "FineUp"); break;
            case 0x09: held.Remove(unit + "Down"); held.Remove(unit + "FineDown"); break;
            default: return DecodedFrame.Nothing;
        }
        return new DecodedFrame(new HashSet<string>(_heldByUnit.Values.SelectMany(s => s)), DecodedFrame.NoTaps);
    }
}
