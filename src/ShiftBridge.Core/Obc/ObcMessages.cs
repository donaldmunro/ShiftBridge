using System.Text;

namespace ShiftBridge.Core.Obc;

public enum ObcHapticPattern : byte
{
    Stop = 0x00, Short = 0x01, Double = 0x02, Triple = 0x03, Long = 0x04, Success = 0x05, Warning = 0x06, Error = 0x07,
}

/// <summary>Haptique demandé par l'appli : durée en unités de 10 ms, intensité 0x00 = défaut.</summary>
public readonly record struct ObcHaptic(ObcHapticPattern Pattern, byte Duration10Ms, byte Intensity);

/// <summary>Informations envoyées par l'appli d'entraînement (message 0x04). ButtonIds vide = tous les boutons.</summary>
public sealed record ObcAppInfo(string AppId, string AppVersion, IReadOnlyList<byte> ButtonIds);

/// <summary>Encodage/décodage des messages OBC v1, identiques en BLE et en TCP.</summary>
public static class ObcMessages
{
    public const byte TypeButtonState = 0x01;
    public const byte TypeDeviceStatus = 0x02;
    public const byte TypeHaptic = 0x03;
    public const byte TypeAppInfo = 0x04;

    /// <summary>Recommandation de la spec : au plus 9 paires par notification (19 octets).</summary>
    public const int MaxPairsPerMessage = 9;

    /// <summary>Découpe une liste de changements en messages 0x01 de 9 paires maximum.</summary>
    public static IEnumerable<byte[]> EncodeButtonState(IEnumerable<(ObcButtonId Id, byte State)> changes)
    {
        var list = changes.ToList();
        for (int i = 0; i < list.Count; i += MaxPairsPerMessage)
        {
            var chunk = list.Skip(i).Take(MaxPairsPerMessage).ToList();
            var msg = new byte[1 + chunk.Count * 2];
            msg[0] = TypeButtonState;
            for (int j = 0; j < chunk.Count; j++)
            {
                msg[1 + j * 2] = (byte)chunk[j].Id;
                msg[2 + j * 2] = chunk[j].State;
            }
            yield return msg;
        }
    }

    /// <summary>Message 0x02 (TCP uniquement). Batterie 0..100, ou 0xFF si non applicable.</summary>
    public static byte[] EncodeDeviceStatus(int? batteryPercent, bool connected) =>
        [TypeDeviceStatus, batteryPercent is >= 0 and <= 100 ? (byte)batteryPercent.Value : (byte)0xFF, connected ? (byte)1 : (byte)0];

    public static bool TryDecodeHaptic(ReadOnlySpan<byte> data, out ObcHaptic haptic)
    {
        haptic = default;
        if (data.Length < 4 || data[0] != TypeHaptic) return false;
        haptic = new ObcHaptic((ObcHapticPattern)data[1], data[2], data[3]);
        return true;
    }

    public static byte[] EncodeHaptic(ObcHaptic h) => [TypeHaptic, (byte)h.Pattern, h.Duration10Ms, h.Intensity];

    public static bool TryDecodeAppInfo(ReadOnlySpan<byte> data, out ObcAppInfo? info)
    {
        info = null;
        // [0x04, 0x01, idLen, id..., verLen, ver..., count, ids...]
        if (data.Length < 3 || data[0] != TypeAppInfo || data[1] != 0x01) return false;
        int pos = 2;
        int idLen = data[pos++];
        if (idLen > 32 || pos + idLen + 1 > data.Length) return false;
        string appId = Encoding.UTF8.GetString(data.Slice(pos, idLen));
        pos += idLen;
        int verLen = data[pos++];
        if (verLen > 32 || pos + verLen + 1 > data.Length) return false;
        string version = Encoding.UTF8.GetString(data.Slice(pos, verLen));
        pos += verLen;
        int count = data[pos++];
        if (pos + count > data.Length) return false;
        info = new ObcAppInfo(appId, version, data.Slice(pos, count).ToArray());
        return true;
    }

    public static byte[] EncodeAppInfo(ObcAppInfo info)
    {
        var id = Encoding.UTF8.GetBytes(info.AppId);
        var ver = Encoding.UTF8.GetBytes(info.AppVersion);
        var msg = new List<byte> { TypeAppInfo, 0x01, (byte)id.Length };
        msg.AddRange(id);
        msg.Add((byte)ver.Length);
        msg.AddRange(ver);
        msg.Add((byte)info.ButtonIds.Count);
        msg.AddRange(info.ButtonIds);
        return [.. msg];
    }
}
