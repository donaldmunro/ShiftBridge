using Android.Bluetooth;
using Android.Content;
using ShiftBridge.Platform.Ble;
using ShiftBridge.Platform.Obc;

namespace ShiftBridge.Platform.Android;

/// <summary>Bluetooth Android : scan LE, liaisons GATT centrales (BluetoothGatt) et OBC périphérique (BluetoothGattServer).</summary>
public sealed class AndroidBlePlatform : IBlePlatform
{
    private readonly Context _context;

    public AndroidBlePlatform(Context context)
    {
        _context = context.ApplicationContext ?? context;
        Central = new AndroidBleCentral(_context);
    }

    public IBleCentral Central { get; }

    public IBleScanner CreateScanner() => new AndroidBleScanner(_context);

    public Task<bool> IsPeripheralSupportedAsync() => Task.FromResult(AndroidObcPeripheral.IsSupported(_context));

    public IObcTransport CreateObcPeripheral() => new AndroidObcPeripheral(_context);

    /// <summary>Vrai si le Bluetooth est présent et activé.</summary>
    public bool IsBluetoothEnabled => IsBluetoothOn(_context);

    public static bool IsBluetoothOn(Context context)
    {
        try { return AndroidBle.Adapter(context) is { IsEnabled: true }; }
        catch (Java.Lang.SecurityException) { return true; } // permission refusée : le bandeau de permission prend le relais
    }
}

internal static class AndroidBle
{
    public static BluetoothAdapter? Adapter(Context context) =>
        (context.GetSystemService(Context.BluetoothService) as BluetoothManager)?.Adapter;

    public static Guid ToGuid(Java.Util.UUID uuid) => Guid.Parse(uuid.ToString()!);

    public static Java.Util.UUID ToUuid(Guid guid) => Java.Util.UUID.FromString(guid.ToString())!;

    /// <summary>UUID du descripteur de configuration client (CCCD, 0x2902) qui active notifications et indications.</summary>
    public static readonly Guid Cccd = new("00002902-0000-1000-8000-00805f9b34fb");
}
