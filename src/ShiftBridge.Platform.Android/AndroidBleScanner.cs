using Android.Bluetooth.LE;
using Android.Content;
using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Platform.Android;

/// <summary>
/// Scan BLE Android. L'enregistrement de scan (annonce + réponse de scan, octets bruts) est décodé par
/// <see cref="BleAdvertisementParser"/>, ce qui évite de manipuler les SparseArray du SDK.
/// Android limite à 5 démarrages de scan par 30 s : le hub ne démarre le scan qu'au premier abonné.
/// Mode « équilibré » : la découverte d'une manette allumée prend environ une seconde de plus qu'en « faible latence », mais le
/// scan continu (manette éteinte pendant des heures) consomme nettement moins et gêne moins les liaisons déjà ouvertes.
/// </summary>
internal sealed class AndroidBleScanner(Context context) : IBleScanner
{
    private readonly object _lock = new();
    private ScanHandler? _handler;

    public event Action<BleAdvertisement>? Received;
    public event Action<string>? Stopped;

    public bool IsRunning => _handler is not null;

    public void Start()
    {
        lock (_lock)
        {
            if (_handler is not null) return;
            try
            {
                var scanner = AndroidBle.Adapter(context)?.BluetoothLeScanner;
                if (scanner is null) { Stopped?.Invoke("Bluetooth désactivé ou indisponible"); return; }

                var settings = new ScanSettings.Builder()!.SetScanMode(global::Android.Bluetooth.LE.ScanMode.Balanced)!.Build()!;
                _handler = new ScanHandler(this);
                scanner.StartScan(null, settings, _handler);
            }
            catch (Java.Lang.SecurityException)
            {
                _handler = null;
                Stopped?.Invoke("permission Bluetooth refusée (BLUETOOTH_SCAN)");
            }
            catch (Exception ex)
            {
                _handler = null;
                Stopped?.Invoke(ex.Message);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_handler is null) return;
            var h = _handler;
            _handler = null;
            try { AndroidBle.Adapter(context)?.BluetoothLeScanner?.StopScan(h); }
            catch { /* Bluetooth coupé entre-temps : le scan est de toute façon arrêté */ }
        }
    }

    public void Dispose() => Stop();

    private void OnResult(ScanResult result)
    {
        if (!BleAddress.TryParse(result.Device?.Address, out ulong address)) return;
        var record = result.ScanRecord;
        var parsed = BleAdvertisementParser.Parse(record?.GetBytes() ?? []);
        Received?.Invoke(new BleAdvertisement(
            address, parsed.LocalName ?? record?.DeviceName, (short)result.Rssi,
            parsed.ManufacturerData, parsed.ServiceUuids, parsed.ServiceData));
    }

    private void OnFailed(ScanFailure error)
    {
        lock (_lock) _handler = null;
        Stopped?.Invoke($"échec du scan Android ({error})");
    }

    private sealed class ScanHandler(AndroidBleScanner owner) : ScanCallback
    {
        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            if (result is null) return;
            try { owner.OnResult(result); } catch { /* une annonce illisible ne doit pas arrêter le scan */ }
        }

        public override void OnBatchScanResults(IList<ScanResult>? results)
        {
            if (results is null) return;
            foreach (var r in results) OnScanResult(ScanCallbackType.AllMatches, r);
        }

        public override void OnScanFailed(ScanFailure errorCode) => owner.OnFailed(errorCode);
    }
}
