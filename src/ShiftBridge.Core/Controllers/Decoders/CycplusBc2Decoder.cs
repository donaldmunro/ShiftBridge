namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// CYCPLUS BC2 (Nordic UART, notifications TX). Trame d'environ 14 octets : en-tête <c>FE EF FF EE</c>, puis l'état du levier
/// « haut » à l'octet 6 et du levier « bas » à l'octet 7 (0x01 ou 0x04 = enfoncé / appui long, 0x03 = relâché).
/// Une valeur inconnue (0x02 observée sans explication) laisse l'état du levier inchangé.
/// </summary>
public sealed class CycplusBc2Decoder : IButtonDecoder
{
    private bool _up, _down;

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length < 8 || data[0] != 0xFE || data[1] != 0xEF || data[2] != 0xFF || data[3] != 0xEE) return null;
        _up = Next(_up, data[6]);
        _down = Next(_down, data[7]);
        var held = new HashSet<string>();
        if (_up) held.Add("Up");
        if (_down) held.Add("Down");
        return new DecodedFrame(held, DecodedFrame.NoTaps);
    }

    private static bool Next(bool current, byte state) => state switch
    {
        0x01 or 0x04 => true,
        0x00 or 0x03 => false,
        _ => current,
    };
}
