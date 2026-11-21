using System.Runtime.InteropServices;
using ShiftBridge.Core.Input;
using ShiftBridge.Platform.Input;

namespace ShiftBridge.Platform.Windows.Input;

/// <summary>
/// Hook clavier global (WH_KEYBOARD_LL). À installer et retirer depuis le thread qui pompe les messages : Windows appelle le hook
/// dans ce thread, et le retire lui-même si le rappel dépasse le délai (~300 ms). Le rappel ne fait donc que des opérations
/// courtes : décider, noter, passer l'événement à <c>onKey</c> (qui ne fait que mettre en file).
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.HookProc _proc; // champ : le délégué ne doit pas être collecté tant que le hook existe
    private readonly Action<ButtonKey, bool> _onKey;
    private readonly Func<bool> _capturing;
    private readonly Dictionary<int, ButtonKey> _keyCache = [];
    private readonly HashSet<ButtonKey> _held = [];       // touches dont on a signalé l'appui (thread du hook uniquement)
    private readonly HashSet<ButtonKey> _swallowed = [];  // appuis avalés : leur relâchement l'est aussi
    private nint _hook;

    private volatile IReadOnlySet<ButtonKey> _bound = new HashSet<ButtonKey>();
    private volatile bool _suppress = true;

    public KeyboardHook(Action<ButtonKey, bool> onKey, Func<bool> capturing)
    {
        _onKey = onKey; _capturing = capturing;
        _proc = HookProc;
    }

    public bool IsInstalled => _hook != 0;

    public void SetBound(IReadOnlySet<ButtonKey> keys, bool suppress) { _bound = keys; _suppress = suppress; }

    public bool Install()
    {
        if (_hook != 0) return true;
        _hook = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandleW(null), 0);
        return _hook != 0;
    }

    /// <summary>Retire le hook et signale le relâchement de ce qui était tenu.</summary>
    public void Uninstall()
    {
        if (_hook != 0) { Native.UnhookWindowsHookEx(_hook); _hook = 0; }
        foreach (var k in _held) _onKey(k, false);
        _held.Clear();
        _swallowed.Clear();
    }

    private ButtonKey KeyFor(int vk, bool extended)
    {
        int id = vk | (extended ? 0x100 : 0);
        if (!_keyCache.TryGetValue(id, out var key))
            _keyCache[id] = key = ButtonKey.Keyboard(KeyboardKeyNames.NameFor(vk, extended));
        return key;
    }

    private nint HookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        try
        {
            // KBDLLHOOKSTRUCT : vkCode (0), scanCode (4), flags (8)
            int vk = Marshal.ReadInt32(lParam, 0);
            int flags = Marshal.ReadInt32(lParam, 8);

            // Touches simulées par un autre logiciel (bureau à distance, macro…) : on n'y touche pas.
            if ((flags & (Native.LLKHF_INJECTED | Native.LLKHF_LOWER_IL_INJECTED)) != 0)
                return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

            var key = KeyFor(vk, (flags & Native.LLKHF_EXTENDED) != 0);
            if ((flags & Native.LLKHF_UP) == 0)
            {
                bool capture = _capturing();
                if (!capture && !_bound.Contains(key)) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

                if (_held.Add(key)) _onKey(key, true);     // la répétition automatique de l'OS ne re-signale pas
                if (capture || _suppress) { _swallowed.Add(key); return 1; }
            }
            else if (_held.Remove(key))
            {
                _onKey(key, false);
                if (_swallowed.Remove(key)) return 1;
            }
        }
        catch { /* un hook ne doit jamais lever : on laisse passer la touche */ }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
