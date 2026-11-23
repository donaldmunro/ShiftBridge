using ShiftBridge.Core.Trainer;
using ShiftBridge.Platform.Ble;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Security.Cryptography;

namespace ShiftBridge.Platform.Windows;

/// <summary>Scan BLE actif WinRT. Le watcher est recréé à chaque démarrage.</summary>
internal sealed class WinBleScanner : IBleScanner
{
    private readonly object _lock = new();
    private BluetoothLEAdvertisementWatcher? _watcher;

    public event Action<BleAdvertisement>? Received;
    public event Action<string>? Stopped;

    public bool IsRunning => _watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;

    public void Start()
    {
        lock (_lock)
        {
            if (_watcher is { Status: BluetoothLEAdvertisementWatcherStatus.Started or BluetoothLEAdvertisementWatcherStatus.Created }) return;
            var w = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
            w.Received += OnReceived;
            w.Stopped += (_, e) => Stopped?.Invoke(e.Error.ToString());
            _watcher = w;
            w.Start();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_watcher is null) return;
            _watcher.Received -= OnReceived;
            try { _watcher.Stop(); } catch { /* déjà arrêté */ }
            _watcher = null;
        }
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var adv = args.Advertisement;

        var manufacturer = new List<(ushort, byte[])>();
        foreach (var md in adv.ManufacturerData)
        {
            CryptographicBuffer.CopyToByteArray(md.Data, out byte[] data);
            manufacturer.Add((md.CompanyId, data));
        }

        // Données de service (types AD 0x16, 0x20 et 0x21 : UUID de 16, 32 ou 128 bits, puis les données), comme sur Android.
        var serviceData = new Dictionary<Guid, byte[]>();
        foreach (var section in adv.DataSections.Where(s => s.DataType is 0x16 or 0x20 or 0x21))
        {
            CryptographicBuffer.CopyToByteArray(section.Data, out byte[] bytes);
            BleAdvertisementParser.AddServiceData(section.DataType, bytes, serviceData);
        }

        Received?.Invoke(new BleAdvertisement(
            args.BluetoothAddress, adv.LocalName, args.RawSignalStrengthInDBm,
            manufacturer, [.. adv.ServiceUuids], serviceData));
    }

    public void Dispose() => Stop();
}
