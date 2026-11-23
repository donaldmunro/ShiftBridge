namespace ShiftBridge.Core.Controllers.Decoders;

/// <summary>
/// Stages SB20 : trame <c>[type] 00 [masque 16 bits petit-boutiste]</c>. Type 0x01 = touches tenues (masque complet) ;
/// 0x04 et 0x08 = relâchement ; 0x03 (validation en cours d'appui) et les autres types sont ignorés.
/// Masque : 0x01 gauche haut, 0x02 gauche bas, 0x04 gauche tiers, 0x08 droite haut, 0x10 droite bas, 0x20 droite tiers.
/// </summary>
public sealed class StagesSb20Decoder : IButtonDecoder
{
    public static readonly Guid Service = new("0c46be5f-9c22-48ff-ae0e-c6eae1a2f4e5");
    public static readonly Guid Notify = new("0c46be60-9c22-48ff-ae0e-c6eae1a2f4e5");

    private static readonly (int Mask, string Name)[] Bits =
    [
        (0x01, "LeftUp"), (0x02, "LeftDown"), (0x04, "LeftThird"), (0x08, "RightUp"), (0x10, "RightDown"), (0x20, "RightThird"),
    ];

    public DecodedFrame? Decode(Guid characteristic, byte[] data)
    {
        if (data.Length < 4) return null;
        switch (data[0])
        {
            case 0x01:
                int mask = data[2] | (data[3] << 8);
                var held = new HashSet<string>();
                foreach (var (m, name) in Bits) if ((mask & m) != 0) held.Add(name);
                return new DecodedFrame(held, DecodedFrame.NoTaps);
            case 0x04 or 0x08:
                return new DecodedFrame(new HashSet<string>(), DecodedFrame.NoTaps);
            default:
                return DecodedFrame.Nothing;
        }
    }
}
