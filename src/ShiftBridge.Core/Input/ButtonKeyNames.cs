using ShiftBridge.Core.Controllers;
using ShiftBridge.Core.Localization;

namespace ShiftBridge.Core.Input;

/// <summary>Libellés lisibles (langue de l'interface) des touches locales pour l'onglet « Touches » et le journal.</summary>
public static class ButtonKeyNames
{
    // Les textes viennent des ressources (clés Kbd_*, Pad_*, Joy_*) et sont résolus à chaque appel : rien n'est figé au
    // premier accès, la langue courante est toujours respectée.

    /// <summary>Touches clavier ayant un libellé propre (les autres s'affichent sous leur nom, « F13 », « A »…).</summary>
    private static readonly HashSet<string> Keyboard =
    [
        "MediaNextTrack", "MediaPrevTrack", "MediaStop", "MediaPlayPause", "VolumeMute", "VolumeDown", "VolumeUp",
        "BrowserBack", "BrowserForward", "BrowserRefresh", "BrowserHome",
        "Left", "Right", "Up", "Down", "Space", "Enter", "NumEnter", "Escape", "Backspace", "PageUp", "PageDown",
        "Home", "End", "Insert", "Delete", "Play",
    ];

    private static readonly HashSet<string> Pad =
    [
        "LT", "RT", "LB", "RB", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "Start", "Back", "LStick", "RStick",
        "LStickUp", "LStickDown", "LStickLeft", "LStickRight", "RStickUp", "RStickDown", "RStickLeft", "RStickRight",
    ];

    /// <summary>Parties de joystick propres (croix HID → mêmes libellés que la croix des manettes de jeu).</summary>
    private static readonly Dictionary<string, string> JoyPart = new()
    {
        ["HatUp"] = "Pad_DPadUp", ["HatDown"] = "Pad_DPadDown", ["HatLeft"] = "Pad_DPadLeft", ["HatRight"] = "Pad_DPadRight",
        ["Plus"] = "Joy_Plus", ["Minus"] = "Joy_Minus", ["Home"] = "Joy_Home", ["Capture"] = "Joy_Capture",
    };

    /// <summary>Manettes reconnues par leur identifiant fabricant_produit (clé de ressource du nom).</summary>
    private static readonly Dictionary<string, string> KnownJoysticks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["057E_2009"] = "Joy_ProController", ["057E_2006"] = "Joy_JoyConL", ["057E_2007"] = "Joy_JoyConR", ["057E_200E"] = "Joy_JoyConGrip",
    };

    /// <summary>Groupe d'affichage : « Clavier », « Manette », « Joystick ».</summary>
    public static string Group(ButtonKey key)
    {
        if (key.TryAsController(out var kind, out _)) return Loc.Get("Model_" + kind);
        return Loc.Get(key.IsKeyboard ? "Group_Keyboard" : key.IsPad ? "Group_Pad" : key.IsJoystick ? "Group_Joystick" : "Group_Zwift");
    }

    /// <summary>Libellé court sans le groupe (« F13 », « A », « Bouton 3 »).</summary>
    public static string Short(ButtonKey key)
    {
        if (key.TryAsController(out var kind, out var name))
        {
            foreach (var (n, labelKey) in kind.Buttons())
                if (n == name) return Loc.Get(labelKey);
            return name;
        }
        if (key.IsKeyboard)
        {
            var n = key.Id[ButtonKey.KeyboardPrefix.Length..];
            return Keyboard.Contains(n) ? Loc.Get("Kbd_" + n) : n;
        }
        if (key.IsPad)
        {
            var n = key.Id[ButtonKey.PadPrefix.Length..];
            return Pad.Contains(n) ? Loc.Get("Pad_" + n) : n;
        }
        if (key.IsJoystick)
        {
            ParseJoystick(key, out _, out var part);
            return PartLabel(part);
        }
        return key.Id;
    }

    /// <summary>Libellé complet (« Clavier : F13 », « Manette : A », « Joystick 046D_C21D : Bouton 3 »).</summary>
    public static string Label(ButtonKey key)
    {
        if (key.IsController) return Loc.Format("Fmt_Label", Group(key), Short(key));
        if (key.IsJoystick)
        {
            ParseJoystick(key, out var dev, out var part);
            var name = KnownJoysticks.TryGetValue(dev, out var nameKey) ? Loc.Get(nameKey) : Loc.Format("Joy_Generic", dev);
            return Loc.Format("Fmt_Label", name, PartLabel(part));
        }
        return key.IsLocalInput ? Loc.Format("Fmt_Label", Group(key), Short(key)) : key.Id;
    }

    private static void ParseJoystick(ButtonKey key, out string device, out string part)
    {
        var rest = key.Id[ButtonKey.JoystickPrefix.Length..];
        int i = rest.LastIndexOf(':');
        (device, part) = i < 0 ? (rest, "") : (rest[..i], rest[(i + 1)..]);
    }

    private static string PartLabel(string part)
    {
        if (JoyPart.TryGetValue(part, out var resKey)) return Loc.Get(resKey);
        if (Pad.Contains(part)) return Loc.Get("Pad_" + part); // croix et sticks des manettes Nintendo : mêmes noms
        if (part.Length > 1 && part[0] == 'B' && int.TryParse(part.AsSpan(1), out var n)) return Loc.Format("Joy_Button", n);
        if (part.EndsWith("Plus", StringComparison.Ordinal)) return Loc.Format("Joy_AxisPlus", part[..^4]);
        if (part.EndsWith("Minus", StringComparison.Ordinal)) return Loc.Format("Joy_AxisMinus", part[..^5]);
        return part;
    }
}
