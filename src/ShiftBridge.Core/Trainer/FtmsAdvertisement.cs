namespace ShiftBridge.Core.Trainer;

/// <summary>
/// Données de service FTMS (0x1826) dans l'annonce : [Flags (1 octet, bit 0 = machine disponible), Type de machine (2 octets LE)].
/// Type : bit 0 tapis, 1 cross-trainer, 2 stepper, 3 escalier, 4 rameur, 5 vélo d'intérieur.
/// </summary>
public readonly record struct FtmsAdvertisement(bool MachineAvailable, ushort MachineType)
{
    public bool IsIndoorBike => (MachineType & (1 << 5)) != 0;

    public static bool TryParse(ReadOnlySpan<byte> serviceData, out FtmsAdvertisement ad)
    {
        ad = default;
        if (serviceData.Length < 3) return false;
        ad = new FtmsAdvertisement((serviceData[0] & 0x01) != 0, (ushort)(serviceData[1] | (serviceData[2] << 8)));
        return true;
    }
}

/// <summary>Types d'annonce reconnus pour un trainer connecté en Bluetooth.</summary>
[Flags]
public enum TrainerProtocols { None = 0, Ftms = 1, FecOverBle = 2, CyclingPower = 4 }

public static class TrainerAdvertisementRules
{
    public static readonly Guid FecOverBleService = new("6e40fec1-b5a3-f393-e0a9-e50e24dcca9e");

    /// <summary>Noms courants quand l'annonce ne porte pas les UUID de service (certains trainers les mettent dans la réponse de scan).</summary>
    private static readonly string[] KnownNames =
        ["kickr", "wahoo", "tacx", "neo", "flux", "direto", "suito", "justo", "avanti", "rivo", "zwift hub", "hub", "van rysel",
         "jetblack", "stages", "magene", "thinkrider", "cycplus", "saris", "elite"];

    public static TrainerProtocols FromServiceUuids(IEnumerable<Guid> uuids)
    {
        var p = TrainerProtocols.None;
        foreach (var u in uuids)
        {
            if (u == FtmsCodec.FitnessMachineService) p |= TrainerProtocols.Ftms;
            else if (u == FecOverBleService) p |= TrainerProtocols.FecOverBle;
            else if (u == FtmsCodec.BaseUuid(0x1818)) p |= TrainerProtocols.CyclingPower;
        }
        return p;
    }

    public static bool LooksLikeTrainerName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && KnownNames.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
}
