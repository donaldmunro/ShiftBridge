namespace ShiftBridge.Core.Trainer;

/// <summary>Fitness Machine Feature (0x2ACC) : 8 octets = fonctions de la machine (u32) + cibles supportées (u32), little-endian.</summary>
public sealed record FtmsFeatures(uint MachineFeatures, uint TargetSettingFeatures)
{
    public static bool TryParse(ReadOnlySpan<byte> data, out FtmsFeatures features)
    {
        features = new FtmsFeatures(0, 0);
        if (data.Length < 8) return false;
        features = new FtmsFeatures(BitConverter.ToUInt32(data[..4]), BitConverter.ToUInt32(data.Slice(4, 4)));
        return true;
    }

    // Fonctions de la machine
    public bool HasCadence => (MachineFeatures & (1u << 1)) != 0;
    public bool HasPowerMeasurement => (MachineFeatures & (1u << 14)) != 0;

    // Cibles supportées
    public bool SupportsResistanceTarget => (TargetSettingFeatures & (1u << 2)) != 0;
    public bool SupportsPowerTarget => (TargetSettingFeatures & (1u << 3)) != 0;
    /// <summary>Bit 13 : paramètres de simulation de vélo d'intérieur (commande 0x11). Indispensable au virtual shifting.</summary>
    public bool SupportsIndoorBikeSimulation => (TargetSettingFeatures & (1u << 13)) != 0;

    public string Describe() =>
        $"fonctions=0x{MachineFeatures:X8} cibles=0x{TargetSettingFeatures:X8} " +
        $"[simulation={Yes(SupportsIndoorBikeSimulation)}, puissance cible={Yes(SupportsPowerTarget)}, " +
        $"résistance cible={Yes(SupportsResistanceTarget)}, cadence={Yes(HasCadence)}, puissance mesurée={Yes(HasPowerMeasurement)}]";

    private static string Yes(bool b) => b ? "oui" : "non";
}
