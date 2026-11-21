namespace ShiftBridge.Core.Obc;

/// <summary>
/// Découpe le flux TCP entrant (appli → device) en messages. La spec v1 n'a pas de préfixe de longueur :
/// on déduit la taille du type de message (0x03 = 4 octets, 0x04 = taille variable auto-décrite).
/// Un octet de type inconnu est ignoré pour se resynchroniser.
/// </summary>
public sealed class ObcStreamParser
{
    private readonly List<byte> _buffer = [];

    public IEnumerable<byte[]> Feed(ReadOnlySpan<byte> data)
    {
        _buffer.AddRange(data.ToArray());
        var messages = new List<byte[]>();
        while (_buffer.Count > 0)
        {
            int len = ExpectedLength();
            if (len < 0) { _buffer.RemoveAt(0); continue; } // type inconnu : resynchronisation
            if (len == 0 || _buffer.Count < len) break;      // message incomplet : on attend la suite
            messages.Add(_buffer.GetRange(0, len).ToArray());
            _buffer.RemoveRange(0, len);
        }
        return messages;
    }

    /// <returns>Taille du message complet, 0 si pas encore déterminable, -1 si type inconnu.</returns>
    private int ExpectedLength()
    {
        switch (_buffer[0])
        {
            case ObcMessages.TypeHaptic: return 4;
            case ObcMessages.TypeAppInfo:
                // [04, ver, idLen, id..., verLen, ver..., count, ids...]
                if (_buffer.Count < 3) return 0;
                int idLen = _buffer[2];
                int verLenPos = 3 + idLen;
                if (_buffer.Count <= verLenPos) return 0;
                int countPos = verLenPos + 1 + _buffer[verLenPos];
                if (_buffer.Count <= countPos) return 0;
                return countPos + 1 + _buffer[countPos];
            default: return -1;
        }
    }
}
