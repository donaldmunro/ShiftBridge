using ShiftBridge.Core.Input;

namespace ShiftBridge.Platform.Input;

/// <summary>Une manette XInput (Xbox 360/One/Series et compatibles) : état brut d'une lecture.</summary>
public readonly record struct XInputSnapshot(ushort Buttons, byte LeftTrigger, byte RightTrigger,
    short LeftX, short LeftY, short RightX, short RightY);

/// <summary>
/// Convertit l'état XInput en touches « pad:* ». Garde l'état précédent pour l'hystérésis : une gâchette ou un stick
/// qui oscille autour du seuil ne produit pas une rafale d'appuis/relâchements.
/// </summary>
public sealed class XInputMapper
{
    public const byte TriggerPress = 100, TriggerRelease = 60;            // sur 255
    public const int StickPress = 16000, StickRelease = 10000;             // sur 32767

    private static readonly (ushort Mask, string Name)[] Digital =
    [
        (0x0001, "DPadUp"), (0x0002, "DPadDown"), (0x0004, "DPadLeft"), (0x0008, "DPadRight"),
        (0x0010, "Start"), (0x0020, "Back"), (0x0040, "LStick"), (0x0080, "RStick"),
        (0x0100, "LB"), (0x0200, "RB"),
        (0x1000, "A"), (0x2000, "B"), (0x4000, "X"), (0x8000, "Y"),
    ];

    private HashSet<ButtonKey> _previous = [];

    public IReadOnlySet<ButtonKey> Map(XInputSnapshot s)
    {
        var pressed = new HashSet<ButtonKey>();
        foreach (var (mask, name) in Digital)
            if ((s.Buttons & mask) != 0) pressed.Add(ButtonKey.Pad(name));

        Analog(pressed, "LT", s.LeftTrigger, TriggerPress, TriggerRelease);
        Analog(pressed, "RT", s.RightTrigger, TriggerPress, TriggerRelease);

        Stick(pressed, "LStick", s.LeftX, s.LeftY);
        Stick(pressed, "RStick", s.RightX, s.RightY);

        _previous = pressed;
        return pressed;
    }

    public void Reset() => _previous = [];

    private void Analog(HashSet<ButtonKey> pressed, string name, int value, int press, int release)
    {
        var key = ButtonKey.Pad(name);
        int threshold = _previous.Contains(key) ? release : press;
        if (value >= threshold) pressed.Add(key);
    }

    private void Stick(HashSet<ButtonKey> pressed, string prefix, int x, int y)
    {
        Direction(pressed, prefix + "Right", x);
        Direction(pressed, prefix + "Left", -x);
        Direction(pressed, prefix + "Up", y);   // XInput : Y positif = haut
        Direction(pressed, prefix + "Down", -y);
    }

    private void Direction(HashSet<ButtonKey> pressed, string name, int value) =>
        Analog(pressed, name, value, StickPress, StickRelease);
}

/// <summary>Décodage des croix directionnelles (hat switch) et des axes de joysticks HID génériques.</summary>
public static class HidMapping
{
    /// <summary>
    /// Hat à 8 directions : 0 = haut, puis sens horaire. <paramref name="index"/> = valeur HID déjà ramenée à 0..7 ;
    /// hors de cette plage (position neutre, souvent 8 ou 15, ou -1) = rien.
    /// </summary>
    public static IEnumerable<string> HatDirections(int index) => index switch
    {
        0 => ["HatUp"],
        1 => ["HatUp", "HatRight"],
        2 => ["HatRight"],
        3 => ["HatDown", "HatRight"],
        4 => ["HatDown"],
        5 => ["HatDown", "HatLeft"],
        6 => ["HatLeft"],
        7 => ["HatUp", "HatLeft"],
        _ => [],
    };

    /// <summary>
    /// Ramène la valeur brute d'un hat à 0..7 d'après sa plage logique ; -1 si neutre.
    /// Un hat à 8 positions va de min à min+7 (neutre = hors plage) ; un hat à 4 positions (min..min+3) est étalé sur 0,2,4,6.
    /// </summary>
    public static int HatIndex(int raw, int logicalMin, int logicalMax)
    {
        int span = logicalMax - logicalMin + 1;
        int v = raw - logicalMin;
        if (v < 0 || v >= span) return -1;
        return span switch { 4 => v * 2, 8 => v, _ => -1 };
    }

    /// <summary>
    /// Axe analogique → -1 / 0 / +1 avec hystérésis. <paramref name="normalized"/> : valeur ramenée à 0..1 (centre = 0,5).
    /// Déclenche à ±0,25 du centre, relâche en dessous de ±0,10. <paramref name="current"/> = état précédent (-1, 0, +1).
    /// </summary>
    public static int AxisDirection(double normalized, int current)
    {
        const double press = 0.25, release = 0.10;
        double off = normalized - 0.5;
        if (current > 0 && off >= release) return 1;
        if (current < 0 && off <= -release) return -1;
        return off >= press ? 1 : off <= -press ? -1 : 0;
    }
}
