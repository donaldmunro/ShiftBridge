namespace ShiftBridge.Core.Trainer;

/// <summary>
/// Ce qui est relayé du vrai trainer vers l'appli. En mode « liste d'autorisation », on ne montre que ce dont le proxy a
/// besoin : un canal de contrôle constructeur visible de l'appli pourrait être préféré à FTMS, et la pente réécrite serait
/// alors contournée. Le Wahoo A026E005 est DANS le service 0x1818, d'où le filtrage par caractéristique.
/// En mode « complet », tout est relayé (comportement historique pour DirCon, validé sur KICKR).
/// </summary>
public sealed class GattRelayPolicy(bool full = false)
{
    private static Guid S(ushort u) => FtmsCodec.BaseUuid(u);

    private static readonly Guid Cps = S(0x1818), Csc = S(0x1816), DeviceInfo = S(0x180A), Battery = S(0x180F), HeartRate = S(0x180D);
    private static readonly Guid CpsMeasurement = S(0x2A63), CpsFeature = S(0x2A65), SensorLocation = S(0x2A5D), CscControlPoint = S(0x2A55);

    public bool Full { get; } = full;

    public bool ShouldRelayService(Guid service)
    {
        // Le canal natif Zwift est réservé à ShiftBridge, même en relais complet : l'appli ne doit pas piloter le trainer
        // en natif en parallèle (conflit avec le rapport appliqué par le proxy).
        if (Zwift.ZwiftTrainerCodec.IsZwiftService(service)) return false;
        return Full || service == FtmsCodec.FitnessMachineService || service == Cps || service == Csc
             || service == DeviceInfo || service == Battery || service == HeartRate;
    }

    public bool ShouldRelayCharacteristic(Guid service, Guid characteristic)
    {
        if (Full) return true;
        if (service == Cps) return characteristic == CpsMeasurement || characteristic == CpsFeature || characteristic == SensorLocation;
        if (service == Csc) return characteristic != CscControlPoint;
        return true;
    }

    /// <summary>
    /// Propriétés GATT (bits Bluetooth : Read 0x02, WriteWithoutResponse 0x04, Write 0x08, Notify 0x10, Indicate 0x20)
    /// vers les bits DirCon (Read 0x01, Write 0x02, Notify 0x04, Indicate 0x08).
    /// </summary>
    public static byte GattToDirConProps(uint gatt)
    {
        byte p = 0;
        if ((gatt & 0x02) != 0) p |= 0x01;
        if ((gatt & (0x04 | 0x08)) != 0) p |= 0x02;
        if ((gatt & 0x10) != 0) p |= 0x04;
        if ((gatt & 0x20) != 0) p |= 0x08;
        return p;
    }
}
