namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Magene Onelap Key (5 boutons). Trame d'au moins 11 octets avec <c>b[1]=F5</c> et <c>b[2]=10</c> :
/// <c>b[5]&amp;1F</c> = type d'unité (0 générale, 1 gauche, 2 droite ; au-delà : ignoré),
/// <c>b[8]&amp;1F</c> = masque des clics, <c>b[10]&amp;1F</c> = masque des appuis longs (tenus jusqu'à la trame suivante).
/// Bits : 0x01 centre, 0x02 gauche, 0x04 haut, 0x08 droite, 0x10 bas.
/// </summary>
public sealed class OnelapKeyDecoder : IButtonDecoder
{
    public static readonly Guid Service = new("8ce5cc01-0a4d-11e9-ab14-d663bd873d93");
    public static readonly Guid Notify = new("8ce5cc02-0a4d-11e9-ab14-d663bd873d93");

    private static readonly (int Bit, string Name)[] Bits =
        [(0x01, "Center"), (0x02, "Left"), (0x04, "Up"), (0x08, "Right"), (0x10, "Down")];

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length < 11 || data[1] != 0xF5 || data[2] != 0x10) return null;
        int type = data[5] & 0x1F;
        if (type > 2) return null;
        string prefix = type switch { 1 => "L.", 2 => "R.", _ => "" };

        var taps = new List<string>();
        var heldHere = new HashSet<string>();
        foreach (var (bit, name) in Bits)
        {
            if ((data[8] & bit) != 0) taps.Add(prefix + name);
            if ((data[10] & bit) != 0) heldHere.Add(prefix + name);
        }
        // Chaque unité (générale / gauche / droite) rapporte ses propres appuis longs : une trame de l'une ne doit pas
        // relâcher ceux de l'autre.
        _heldByUnit[type] = heldHere;
        var held = new HashSet<string>(_heldByUnit.Values.SelectMany(s => s));
        return new DecodedFrame(held, taps);
    }

    private readonly Dictionary<int, HashSet<string>> _heldByUnit = [];
}
