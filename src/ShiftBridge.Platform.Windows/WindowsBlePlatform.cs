using ShiftBridge.Platform.Ble;
using ShiftBridge.Platform.Obc;

namespace ShiftBridge.Platform.Windows;

/// <summary>Bluetooth Windows (WinRT) : scan actif, liaisons GATT centrales et OBC périphérique.</summary>
public sealed class WindowsBlePlatform : IBlePlatform
{
    public IBleCentral Central { get; } = new WinBleCentral();

    public IBleScanner CreateScanner() => new WinBleScanner();

    public Task<bool> IsPeripheralSupportedAsync() => ObcBlePeripheral.IsSupportedAsync();

    public IObcTransport CreateObcPeripheral() => new ObcBlePeripheral();
}
