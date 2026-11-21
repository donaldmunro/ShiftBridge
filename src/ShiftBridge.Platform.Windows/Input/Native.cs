using System.Runtime.InteropServices;

namespace ShiftBridge.Platform.Windows.Input;

/// <summary>P/Invoke Win32 : fenêtre de messages, hook clavier bas niveau, Raw Input, HID et XInput.</summary>
internal static class Native
{
    public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);
    public delegate nint HookProc(int nCode, nint wParam, nint lParam);

    public const int WH_KEYBOARD_LL = 13;
    public const uint WM_DESTROY = 0x0002, WM_INPUT_DEVICE_CHANGE = 0x00FE, WM_INPUT = 0x00FF, WM_APP = 0x8000;
    public const int LLKHF_EXTENDED = 0x01, LLKHF_LOWER_IL_INJECTED = 0x02, LLKHF_INJECTED = 0x10, LLKHF_UP = 0x80;
    public static readonly nint HWND_MESSAGE = new(-3);

    // ----- user32 : fenêtre et boucle de messages -----

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public nint hwnd; public uint message; public nint wParam; public nint lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize; public uint style; public nint lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
        public nint hInstance; public nint hIcon; public nint hCursor; public nint hbrBackground;
        public string? lpszMenuName; public string lpszClassName; public nint hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool UnregisterClassW(string className, nint hInstance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint CreateWindowExW(uint exStyle, string cls, string? name, uint style, int x, int y, int w, int h,
        nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern int GetMessageW(out MSG msg, nint hwnd, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern nint DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] public static extern bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern void PostQuitMessage(int code);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandleW(string? name);

    // ----- user32 : hook clavier -----

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookExW(int idHook, HookProc proc, nint hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] public static extern nint CallNextHookEx(nint hook, int nCode, nint wParam, nint lParam);

    // ----- user32 : Raw Input -----

    public const uint RIDEV_REMOVE = 0x01, RIDEV_INPUTSINK = 0x100, RIDEV_DEVNOTIFY = 0x2000;
    public const uint RID_INPUT = 0x10000003;
    public const uint RIDI_PREPARSEDDATA = 0x20000005, RIDI_DEVICENAME = 0x20000007;
    public const uint RIM_TYPEHID = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE { public ushort UsagePage; public ushort Usage; public uint Flags; public nint Target; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICELIST { public nint Device; public uint Type; }

    /// <summary>En-tête de RAWINPUT : 24 octets en 64 bits, 16 en 32 bits.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER { public uint Type; public uint Size; public nint Device; public nint WParam; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")]
    public static extern uint GetRawInputData(nint rawInput, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll")]
    public static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[]? list, ref uint count, uint size);
    [DllImport("user32.dll")]
    public static extern uint GetRawInputDeviceInfoW(nint device, uint command, nint data, ref uint size);

    // ----- hid.dll -----

    public const int HidP_Input = 0;
    public const int HIDP_STATUS_SUCCESS = 0x110000;

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        public ushort R0, R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13, R14, R15, R16;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    /// <summary>HIDP_VALUE_CAPS (72 octets). L'union finale est vue comme « plage » ; hors plage, Usage = UsageMin.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_VALUE_CAPS
    {
        public ushort UsagePage; public byte ReportID; public byte IsAlias;
        public ushort BitField, LinkCollection, LinkUsage, LinkUsagePage;
        public byte IsRange, IsStringRange, IsDesignatorRange, IsAbsolute, HasNull, Reserved;
        public ushort BitSize, ReportCount;
        public ushort R0, R1, R2, R3, R4;
        public uint UnitsExp, Units;
        public int LogicalMin, LogicalMax, PhysicalMin, PhysicalMax;
        public ushort UsageMin, UsageMax, StringMin, StringMax, DesignatorMin, DesignatorMax, DataIndexMin, DataIndexMax;
    }

    [DllImport("hid.dll")] public static extern int HidP_GetCaps(nint preparsed, out HIDP_CAPS caps);
    [DllImport("hid.dll")]
    public static extern int HidP_GetValueCaps(int type, [Out] HIDP_VALUE_CAPS[] caps, ref ushort length, nint preparsed);
    [DllImport("hid.dll")]
    public static extern uint HidP_MaxUsageListLength(int type, ushort usagePage, nint preparsed);
    [DllImport("hid.dll")]
    public static extern int HidP_GetUsages(int type, ushort usagePage, ushort linkCollection, [Out] ushort[] usageList,
        ref uint usageLength, nint preparsed, byte[] report, uint reportLength);
    [DllImport("hid.dll")]
    public static extern int HidP_GetUsageValue(int type, ushort usagePage, ushort linkCollection, ushort usage,
        out uint value, nint preparsed, byte[] report, uint reportLength);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    public static extern bool HidD_GetProductString(nint handle, [Out] char[] buffer, uint bufferLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(nint handle);
    public static readonly nint INVALID_HANDLE_VALUE = new(-1);

    // ----- winmm : résolution du minuteur (le sondage XInput vise ~8 ms) -----

    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

    // ----- XInput -----

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint PacketNumber;
        public ushort Buttons; public byte LeftTrigger; public byte RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_VIBRATION { public ushort Left, Right; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    public static extern uint XInputGetState(uint index, out XINPUT_STATE state);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    public static extern uint XInputSetState(uint index, ref XINPUT_VIBRATION vibration);
}
