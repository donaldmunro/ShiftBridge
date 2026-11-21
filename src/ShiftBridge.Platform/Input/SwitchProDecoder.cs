using System.Text.RegularExpressions;
using ShiftBridge.Core.Input;

namespace ShiftBridge.Platform.Input;

/// <summary>Extraction de l'identifiant fabricant/produit d'un chemin de périphérique HID Windows.</summary>
public static partial class HidIds
{
    // USB : « VID_046D&PID_C21D ». Bluetooth classique : « VID&0002057e_PID&2009 » (source du VID sur 4 chiffres, puis le VID).
    [GeneratedRegex(@"VID[_&]([0-9A-F]{4,8}).*?PID[_&]([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidPidRegex();

    public static (string Vid, string Pid) Parse(string devicePath)
    {
        var m = VidPidRegex().Match(devicePath);
        if (!m.Success) return ("0000", "0000");
        var vid = m.Groups[1].Value;
        return (vid[^4..].ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant());
    }
}

/// <summary>
/// Manettes Nintendo Switch (Pro Controller, Joy-Con) en Bluetooth. Dès qu'une autre appli (Steam…) a passé la manette en
/// « rapport complet », elle n'envoie plus le rapport simple décrit par son descripteur HID : les boutons sont dans un rapport
/// 0x30 de 362 octets que le décodage HID générique ne sait pas lire. On le décode ici, octets de boutons 3 à 5 (vérifié sur
/// des rapports réels : A = 0x08, B = 0x04, X = 0x02, Y = 0x01, R = 0x40, ZR = 0x80 à l'octet 3 ; L = 0x40, ZL = 0x80 à l'octet 5).
/// </summary>
public sealed class SwitchProDecoder
{
    private const string NintendoVid = "057E";
    private static readonly HashSet<string> Pids = ["2009", "2006", "2007", "200E"]; // Pro Controller, Joy-Con G/D, grip

    // Rapports qui portent l'état des boutons aux octets 3 à 5 : 0x30 (standard), 0x31 (NFC/IR), 0x21 (réponse de commande).
    private static readonly HashSet<byte> ButtonReports = [0x30, 0x31, 0x21];

    private const int StickPress = 1000, StickRelease = 700; // autour du centre (≈2048), plage utile ≈ ±1500

    private static readonly (int Byte, byte Mask, string Name)[] Digital =
    [
        (3, 0x01, "Y"), (3, 0x02, "X"), (3, 0x04, "B"), (3, 0x08, "A"), (3, 0x40, "R"), (3, 0x80, "ZR"),
        (4, 0x01, "Minus"), (4, 0x02, "Plus"), (4, 0x04, "RStick"), (4, 0x08, "LStick"), (4, 0x10, "Home"), (4, 0x20, "Capture"),
        (5, 0x01, "DPadDown"), (5, 0x02, "DPadUp"), (5, 0x04, "DPadRight"), (5, 0x08, "DPadLeft"), (5, 0x40, "L"), (5, 0x80, "ZL"),
    ];

    private readonly string _deviceId;
    private HashSet<ButtonKey> _previous = [];

    public SwitchProDecoder(string deviceId) => _deviceId = deviceId;

    public static bool Supports(string vid, string pid) =>
        string.Equals(vid, NintendoVid, StringComparison.OrdinalIgnoreCase) && Pids.Contains(pid.ToUpperInvariant());

    /// <summary>Vrai si ce rapport est de ceux que ce décodeur sait lire (sinon, on retombe sur le décodage HID générique).</summary>
    public static bool CanDecode(ReadOnlySpan<byte> report) => report.Length >= 12 && ButtonReports.Contains(report[0]);

    /// <summary>Touches actuellement enfoncées d'après un rapport complet. À n'appeler qu'avec <see cref="CanDecode"/> vrai.</summary>
    public IReadOnlySet<ButtonKey> Decode(ReadOnlySpan<byte> report)
    {
        var pressed = new HashSet<ButtonKey>();
        foreach (var (index, mask, name) in Digital)
            if ((report[index] & mask) != 0) pressed.Add(ButtonKey.Joystick(_deviceId, name));

        // Sticks : 12 bits par axe, X puis Y, 3 octets par stick (gauche 6..8, droit 9..11).
        Stick(pressed, "LStick", report[6], report[7], report[8]);
        Stick(pressed, "RStick", report[9], report[10], report[11]);

        _previous = pressed;
        return pressed;
    }

    public void Reset() => _previous = [];

    private void Stick(HashSet<ButtonKey> pressed, string prefix, byte b0, byte b1, byte b2)
    {
        int x = (b0 | ((b1 & 0x0F) << 8)) - 2048;
        int y = ((b1 >> 4) | (b2 << 4)) - 2048;
        Direction(pressed, prefix + "Right", x);
        Direction(pressed, prefix + "Left", -x);
        Direction(pressed, prefix + "Up", y);   // y croît vers le haut
        Direction(pressed, prefix + "Down", -y);
    }

    private void Direction(HashSet<ButtonKey> pressed, string name, int value)
    {
        var key = ButtonKey.Joystick(_deviceId, name);
        if (value >= (_previous.Contains(key) ? StickRelease : StickPress)) pressed.Add(key);
    }
}
