using ShiftBridge.Core.Zwift;
using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Platform.Controllers;

public sealed record DiscoveredZwiftDevice(ulong Address, string Name, ZwiftDeviceType Type, short Rssi, byte[] ManufacturerData)
{
    public string AddressText => BleAddress.ToText(Address);
}

/// <summary>Scan BLE filtré sur le company id Zwift (0x094A), via le hub d'annonces partagé.</summary>
public sealed class ZwiftScanner(BleAdvertisementHub hub) : IDisposable
{
    private IDisposable? _subscription;

    public bool IsScanning => _subscription is not null;

    /// <summary>Levé à chaque annonce reçue (la même manette plusieurs fois : l'appelant déduplique par adresse).</summary>
    public event Action<DiscoveredZwiftDevice>? DeviceSeen;
    public event Action<string>? Log;

    public void Start()
    {
        if (_subscription is not null) return;
        _subscription = hub.Subscribe(OnReceived);
        Log?.Invoke("Recherche des manettes Zwift démarrée");
    }

    public void Stop()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private void OnReceived(BleAdvertisement adv)
    {
        foreach (var (companyId, data) in adv.ManufacturerData)
        {
            if (companyId != ZwiftConstants.ManufacturerId) continue;
            var type = ZwiftDeviceType.FromManufacturerData(data);
            string baseName = string.IsNullOrWhiteSpace(adv.LocalName) ? $"Zwift {type.Model}" : adv.LocalName;
            // Deux manettes du même modèle (gauche/droite) doivent être distinguables dans le journal.
            string side = type.Side switch { ZwiftSide.Left => "G", ZwiftSide.Right => "D", _ => "" };
            string name = $"{baseName} {side}{(adv.Address & 0xFFFF):X4}".Replace("  ", " ");
            DeviceSeen?.Invoke(new DiscoveredZwiftDevice(adv.Address, name, type, adv.Rssi, data));
        }
    }

    public void Dispose() => Stop();
}
