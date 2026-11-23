using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Platform.Controllers;

/// <summary>Cherche toutes les manettes BLE prises en charge (Zwift et autres) via le hub d'annonces partagé.</summary>
public sealed class ControllerScanner(BleAdvertisementHub hub) : IDisposable
{
    private IDisposable? _subscription;

    public bool IsScanning => _subscription is not null;

    /// <summary>Levé à chaque annonce d'une manette reconnue (l'appelant déduplique par adresse).</summary>
    public event Action<DiscoveredController>? DeviceSeen;
    public event Action<string>? Log;

    public void Start()
    {
        if (_subscription is not null) return;
        _subscription = hub.Subscribe(OnReceived);
        Log?.Invoke("Recherche des manettes démarrée");
    }

    public void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void OnReceived(BleAdvertisement adv)
    {
        foreach (var z in ZwiftScanner.Detect(adv)) DeviceSeen?.Invoke(z);
        if (ControllerCatalog.Detect(adv) is { } other) DeviceSeen?.Invoke(other);
    }

    public void Dispose() => Stop();
}
