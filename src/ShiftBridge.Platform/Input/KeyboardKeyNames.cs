namespace ShiftBridge.Platform.Input;

/// <summary>Code de touche virtuelle Windows (VK_*) → nom stable utilisé dans « kbd:&lt;nom&gt; » (jamais traduit : il est enregistré).</summary>
public static class KeyboardKeyNames
{
    private static readonly Dictionary<int, string> Named = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x14] = "CapsLock",
        [0x1B] = "Escape", [0x20] = "Space", [0x21] = "PageUp", [0x22] = "PageDown", [0x23] = "End", [0x24] = "Home",
        [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down", [0x2C] = "PrintScreen", [0x2D] = "Insert",
        [0x2E] = "Delete", [0x5D] = "Menu",
        [0x6A] = "NumMultiply", [0x6B] = "NumAdd", [0x6D] = "NumSubtract", [0x6E] = "NumDecimal", [0x6F] = "NumDivide",
        [0x90] = "NumLock", [0x91] = "ScrollLock",
        [0xA0] = "LShift", [0xA1] = "RShift", [0xA2] = "LCtrl", [0xA3] = "RCtrl", [0xA4] = "LAlt", [0xA5] = "RAlt",
        [0x5B] = "LWin", [0x5C] = "RWin",
        [0xA6] = "BrowserBack", [0xA7] = "BrowserForward", [0xA8] = "BrowserRefresh", [0xAC] = "BrowserHome",
        [0xAD] = "VolumeMute", [0xAE] = "VolumeDown", [0xAF] = "VolumeUp",
        [0xB0] = "MediaNextTrack", [0xB1] = "MediaPrevTrack", [0xB2] = "MediaStop", [0xB3] = "MediaPlayPause",
        [0xBA] = "Semicolon", [0xBB] = "Equals", [0xBC] = "Comma", [0xBD] = "Minus", [0xBE] = "Period",
        [0xBF] = "Slash", [0xC0] = "Backtick", [0xDB] = "LeftBracket", [0xDC] = "Backslash",
        [0xDD] = "RightBracket", [0xDE] = "Quote",
        [0xFA] = "Play", [0xFB] = "Zoom",
    };

    /// <summary>
    /// Nom d'une touche. <paramref name="extended"/> distingue Entrée du pavé numérique ; les autres touches étendues
    /// (flèches, Inser…) ont déjà un code différent de leur équivalent pavé.
    /// </summary>
    public static string NameFor(int vk, bool extended = false)
    {
        if (vk == 0x0D && extended) return "NumEnter";
        if (vk is >= 0x41 and <= 0x5A) return ((char)vk).ToString();             // A–Z
        if (vk is >= 0x30 and <= 0x39) return ((char)vk).ToString();             // 0–9 (rangée du haut)
        if (vk is >= 0x60 and <= 0x69) return "Num" + (vk - 0x60);               // pavé numérique
        if (vk is >= 0x70 and <= 0x87) return "F" + (vk - 0x6F);                 // F1–F24
        return Named.TryGetValue(vk, out var n) ? n : $"VK{vk:X2}";
    }
}
