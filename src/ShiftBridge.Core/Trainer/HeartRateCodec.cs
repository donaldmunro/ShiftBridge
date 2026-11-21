namespace ShiftBridge.Core.Trainer;

/// <summary>Mesure de fréquence cardiaque (caractéristique 0x2A37). <paramref name="Contact"/> : null si le capteur ne gère pas la détection de contact.</summary>
public readonly record struct HeartRateMeasurement(int Bpm, bool? Contact, int? EnergyKj, int RrCount);

/// <summary>Service Heart Rate (0x180D) : UUID et décodage de la mesure (flags : bit 0 = BPM sur 16 bits, bits 1-2 = contact, bit 3 = énergie, bit 4 = intervalles RR).</summary>
public static class HeartRateCodec
{
    public static readonly Guid Service = FtmsCodec.BaseUuid(0x180D);
    public static readonly Guid Measurement = FtmsCodec.BaseUuid(0x2A37);
    public static readonly Guid SensorLocation = FtmsCodec.BaseUuid(0x2A38);

    /// <summary>Position « poitrine » (0x01), renvoyée quand le capteur n'expose pas la sienne.</summary>
    public const byte LocationChest = 0x01;

    public static bool IsHeartRateSensor(IEnumerable<Guid> serviceUuids) => serviceUuids.Contains(Service);

    public static bool TryParse(ReadOnlySpan<byte> data, out HeartRateMeasurement m)
    {
        m = default;
        if (data.Length < 2) return false;
        byte flags = data[0];
        bool wide = (flags & 0x01) != 0;
        int i = 1;
        if (data.Length < i + (wide ? 2 : 1)) return false;
        int bpm = wide ? data[i] | (data[i + 1] << 8) : data[i];
        i += wide ? 2 : 1;

        int contactBits = (flags >> 1) & 0x03; // 0/1 : non géré ; 2 : pas de contact ; 3 : contact
        bool? contact = contactBits >= 2 ? contactBits == 3 : null;

        int? energy = null;
        if ((flags & 0x08) != 0)
        {
            if (data.Length < i + 2) return false;
            energy = data[i] | (data[i + 1] << 8);
            i += 2;
        }

        int rr = 0;
        if ((flags & 0x10) != 0) rr = (data.Length - i) / 2;

        m = new HeartRateMeasurement(bpm, contact, energy, rr);
        return true;
    }
}
