namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Shimano Di2 via D-Fly (EW-WU111, dérailleur arrière sans fil RDR) : indications sur 0x2AC2 du service 0x18EF (base « SHIMANO_BLE »).
/// Trame : <c>[compteur][canal 1][canal 2][canal 3][canal 4]</c>. Par canal : quartet haut 0xF = repos ; bit 0x10 = appui court ;
/// 0x20 = appui long (tenu) ; 0x40 = double appui. Seuls les CHANGEMENTS comptent ; un 0x1N qui suit un appui long = toujours tenu.
/// La première trame sert de référence, sauf si elle porte déjà un appui (compteur 0x20 sur une liaison neuve).
/// Il faut avoir affecté les palettes à des canaux D-Fly dans l'appli E-Tube de Shimano.
/// </summary>
public sealed class ShimanoDi2Decoder : IButtonDecoder
{
    public static readonly Guid Service = new("000018ef-5348-494d-414e-4f5f424c4500");
    public static readonly Guid ConfigService = new("000018ff-5348-494d-414e-4f5f424c4500");
    public static readonly Guid Channels = new("00002ac2-5348-494d-414e-4f5f424c4500");

    private const int ChannelCount = 4;
    private readonly byte?[] _previous = new byte?[ChannelCount];
    private readonly bool[] _longHeld = new bool[ChannelCount];
    private bool _first = true;

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length < 2) return null;
        bool baseline = false;
        if (_first)
        {
            _first = false;
            baseline = data[0] != 0x20; // une liaison neuve dont la 1re trame porte déjà un appui : on le traite
        }

        var taps = new List<string>();
        int n = Math.Min(ChannelCount, data.Length - 1);
        for (int i = 0; i < n; i++)
        {
            byte v = data[i + 1];
            byte prev = _previous[i] ?? 0xF0;
            _previous[i] = v;
            if (baseline || v == prev) continue;
            string name = "Ch" + (i + 1);

            if ((v & 0xF0) == 0xF0) _longHeld[i] = false;                    // repos
            else if ((v & 0x10) != 0) { if (!_longHeld[i]) taps.Add(name); } // court (ou maintien d'un appui long)
            else if ((v & 0x20) != 0) _longHeld[i] = true;                   // long
            else if ((v & 0x40) != 0) { _longHeld[i] = false; taps.Add(name); taps.Add(name); } // double
            else _longHeld[i] = false;
        }

        var held = new HashSet<string>();
        for (int i = 0; i < ChannelCount; i++) if (_longHeld[i]) held.Add("Ch" + (i + 1));
        return new DecodedFrame(held, taps);
    }
}
