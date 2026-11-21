using ShiftBridge.Core.Protobuf;
using ShiftBridge.Core.Trainer;

namespace ShiftBridge.Core.Zwift;

/// <summary>Données de conduite reçues du trainer sur le canal Zwift (opcode 0x03).</summary>
public readonly record struct ZwiftRidingData(uint Power, uint Cadence, uint SpeedX100, uint HeartRate);

/// <summary>
/// Protocole NATIF Zwift des trainers « Zwift Ready » (virtual shifting appliqué par le trainer lui-même), d'après les
/// travaux publics de rétro-ingénierie : Makinolo (« Zwift trainer protocol »), TrainerHUD (MIT) et serpentine (MIT).
/// Rien de ceci n'est publié par Zwift. Trame = 1 octet d'opcode + message protobuf, sans longueur ni CRC, sans chiffrement.
///
/// Commande 0x04 (HubCommand) : champ 3 = cible de puissance ; champ 4 = Simulation {1 vent sint ×100 m/s, 2 pente sint ×100 %,
/// 3 CWa ×10 000, 4 Crr ×100 000} ; champ 5 = Physical {2 rapport ×10 000, 4 poids vélo ×100 kg, 5 poids coureur ×100 kg}.
/// </summary>
public static class ZwiftTrainerCodec
{
    public const byte OpRequest = 0x00;
    public const byte OpRidingData = 0x03;
    public const byte OpCommand = 0x04;

    /// <summary>« RideOn » + 02 01 : ouverture de session côté TRAINER (le 01 02 des manettes n'est pas le même).</summary>
    public static readonly byte[] RideOnTrainer = [.. ZwiftConstants.RideOn, 0x02, 0x01];
    public static readonly byte[] InitFrame = [0x41, 0x08, 0x05];
    public static readonly byte[] InfoRequest = [0x00, 0x08, 0x00];
    /// <summary>Demande le rapport courant (paramètre 520).</summary>
    public static readonly byte[] GearQuery = [0x00, 0x08, 0x88, 0x04];

    public static bool IsZwiftService(Guid service) =>
        service == ZwiftConstants.CustomService || service == ZwiftConstants.RideCustomService;

    /// <summary>La réponse au handshake commence par « RideOn » (le reste de la réponse n'est capturé par aucune source).</summary>
    public static bool IsRideOnReply(ReadOnlySpan<byte> data) =>
        data.Length >= ZwiftConstants.RideOn.Length && data[..ZwiftConstants.RideOn.Length].SequenceEqual(ZwiftConstants.RideOn);

    /// <summary>
    /// Simulation. Équivalences avec la commande FTMS : pente = même unité (0,01 %) ; CWa = cw brut ×100 (51 → 5100) ;
    /// Crr = crr brut ×10 (40 → 400) ; vent = vent FTMS brut (0,001 m/s) ÷ 10. <paramref name="full"/> = false pour le simple
    /// maintien (vent et pente seulement).
    /// </summary>
    public static byte[] EncodeSimulation(SimulationParams p, bool full)
    {
        var sim = new ProtoWriter()
            .SInt(1, (long)Math.Round(p.WindRaw / 10.0))
            .SInt(2, p.GradeRaw);
        if (full)
            sim.Varint(3, (ulong)p.CwRaw * 100).Varint(4, (ulong)p.CrrRaw * 10);
        return [OpCommand, .. new ProtoWriter().Message(4, sim).ToArray()];
    }

    /// <summary>Rapport de vitesse (et poids). Un rapport 0 libère le virtual shifting.</summary>
    public static byte[] EncodePhysical(double? gearRatio, double? bikeKg = null, double? riderKg = null)
    {
        var phys = new ProtoWriter();
        if (gearRatio is { } g) phys.Varint(2, (ulong)Math.Max(0, Math.Round(g * 10000)));
        if (bikeKg is { } b) phys.Varint(4, (ulong)Math.Max(0, Math.Round(b * 100)));
        if (riderKg is { } r) phys.Varint(5, (ulong)Math.Max(0, Math.Round(r * 100)));
        return [OpCommand, .. new ProtoWriter().Message(5, phys).ToArray()];
    }

    /// <summary>Opcode des réponses du trainer sur 0004 (infos, rapport…).</summary>
    public const byte OpResponse = 0x3C;
    public const int GearRatioParameter = 520;

    /// <summary>
    /// Réponse à la requête de rapport (<see cref="GearQuery"/>). Structure observée sur un KICKR :
    /// 3C { 1 = 520 ; 2 = { 1 = { 8 = rapport ×10 000 } } }, p. ex. 3C 08 88 04 12 06 0A 04 40 C0 BB 01 = 2,40.
    /// </summary>
    public static bool TryDecodeGearReply(ReadOnlySpan<byte> data, out double ratio)
    {
        ratio = 0;
        if (data.Length < 2 || data[0] != OpResponse) return false;
        var top = ProtoReader.ReadFields(data[1..]);
        if (!top.Any(f => f.Number == 1 && f.WireType == ProtoField.WireVarint && f.Varint == GearRatioParameter)) return false;

        var outer = top.FirstOrDefault(f => f.Number == 2 && f.WireType == ProtoField.WireLengthDelimited);
        if (outer.Number != 2) return false;
        var mid = ProtoReader.ReadFields(outer.Bytes.Span).FirstOrDefault(f => f.Number == 1 && f.WireType == ProtoField.WireLengthDelimited);
        if (mid.Number != 1) return false;
        var value = ProtoReader.ReadFields(mid.Bytes.Span).FirstOrDefault(f => f.Number == 8 && f.WireType == ProtoField.WireVarint);
        if (value.Number != 8) return false;

        ratio = value.Varint / 10000.0;
        return true;
    }

    public static bool TryDecodeRidingData(ReadOnlySpan<byte> data, out ZwiftRidingData ride)
    {
        ride = default;
        if (data.Length < 2 || data[0] != OpRidingData) return false;
        uint power = 0, cadence = 0, speed = 0, hr = 0;
        foreach (var f in ProtoReader.ReadFields(data[1..]))
        {
            if (f.WireType != ProtoField.WireVarint) continue;
            switch (f.Number)
            {
                case 1: power = (uint)f.Varint; break;
                case 2: cadence = (uint)f.Varint; break;
                case 3: speed = (uint)f.Varint; break;
                case 4: hr = (uint)f.Varint; break;
            }
        }
        ride = new ZwiftRidingData(power, cadence, speed, hr);
        return true;
    }
}
