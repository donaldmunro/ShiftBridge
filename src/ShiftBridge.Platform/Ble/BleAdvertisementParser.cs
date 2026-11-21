using ShiftBridge.Core.Trainer;

namespace ShiftBridge.Platform.Ble;

/// <summary>
/// Décodage des structures AD (« Advertising Data ») d'une annonce BLE brute, pour les plateformes qui fournissent les octets
/// bruts (Android). Format : suite de [longueur, type, données…], la longueur comptant le type. Tolérant : une structure
/// tronquée ou mal formée arrête le décodage sans exception, en gardant ce qui a déjà été lu.
/// </summary>
public static class BleAdvertisementParser
{
    public sealed record Result(
        string? LocalName,
        IReadOnlyList<(ushort CompanyId, byte[] Data)> ManufacturerData,
        IReadOnlyList<Guid> ServiceUuids,
        IReadOnlyDictionary<Guid, byte[]> ServiceData);

    private const byte ShortenedName = 0x08, CompleteName = 0x09;
    private const byte Uuid16Incomplete = 0x02, Uuid16Complete = 0x03;
    private const byte Uuid32Incomplete = 0x04, Uuid32Complete = 0x05;
    private const byte Uuid128Incomplete = 0x06, Uuid128Complete = 0x07;
    private const byte ServiceData16 = 0x16, ServiceData32 = 0x20, ServiceData128 = 0x21;
    private const byte Manufacturer = 0xFF;

    public static Result Parse(ReadOnlySpan<byte> raw)
    {
        string? shortName = null, fullName = null;
        var manufacturer = new List<(ushort, byte[])>();
        var uuids = new List<Guid>();
        var serviceData = new Dictionary<Guid, byte[]>();

        int i = 0;
        while (i < raw.Length)
        {
            int len = raw[i];
            if (len == 0) break;                          // remplissage : fin des données significatives
            if (i + 1 + len > raw.Length) break;          // structure tronquée
            byte type = raw[i + 1];
            var data = raw.Slice(i + 2, len - 1);
            i += 1 + len;

            switch (type)
            {
                case ShortenedName: shortName = DecodeName(data); break;
                case CompleteName: fullName = DecodeName(data); break;

                case Uuid16Incomplete or Uuid16Complete:
                    for (int o = 0; o + 2 <= data.Length; o += 2) uuids.Add(FtmsCodec.BaseUuid(ReadU16(data[o..])));
                    break;
                case Uuid32Incomplete or Uuid32Complete:
                    for (int o = 0; o + 4 <= data.Length; o += 4) uuids.Add(FromU32(ReadU32(data[o..])));
                    break;
                case Uuid128Incomplete or Uuid128Complete:
                    for (int o = 0; o + 16 <= data.Length; o += 16) uuids.Add(FromLittleEndian128(data.Slice(o, 16)));
                    break;

                case ServiceData16 when data.Length >= 2:
                    serviceData[FtmsCodec.BaseUuid(ReadU16(data))] = data[2..].ToArray();
                    break;
                case ServiceData32 when data.Length >= 4:
                    serviceData[FromU32(ReadU32(data))] = data[4..].ToArray();
                    break;
                case ServiceData128 when data.Length >= 16:
                    serviceData[FromLittleEndian128(data[..16])] = data[16..].ToArray();
                    break;

                case Manufacturer when data.Length >= 2:
                    manufacturer.Add((ReadU16(data), data[2..].ToArray()));
                    break;
            }
        }

        return new Result(fullName ?? shortName, manufacturer, uuids, serviceData);
    }

    private static string? DecodeName(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return null;
        // Le nom est de l'UTF-8 ; certains appareils le terminent par un octet nul.
        string s = System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0');
        return s.Length == 0 ? null : s;
    }

    private static ushort ReadU16(ReadOnlySpan<byte> d) => (ushort)(d[0] | (d[1] << 8));
    private static uint ReadU32(ReadOnlySpan<byte> d) => (uint)(d[0] | (d[1] << 8) | (d[2] << 16) | (d[3] << 24));

    private static Guid FromU32(uint v) => new($"{v:x8}-0000-1000-8000-00805f9b34fb");

    /// <summary>Les UUID 128 bits sont transmis octet de poids faible en premier : on les remet dans l'ordre d'affichage.</summary>
    private static Guid FromLittleEndian128(ReadOnlySpan<byte> d)
    {
        Span<byte> be = stackalloc byte[16];
        for (int k = 0; k < 16; k++) be[k] = d[15 - k];
        string h = Convert.ToHexString(be).ToLowerInvariant();
        return new Guid($"{h[..8]}-{h[8..12]}-{h[12..16]}-{h[16..20]}-{h[20..]}");
    }
}
