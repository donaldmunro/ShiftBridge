using ShiftBridge.Core.Input;

namespace ShiftBridge.Platform.Controllers;

public enum ControllerState { Disconnected, Connecting, Connected, Locked, Error }

/// <summary>Une manette BLE connectée ou en cours de connexion (Zwift ou autre) : ce que le chef d'orchestre et l'interface en voient.</summary>
public interface IController : IAsyncDisposable
{
    DiscoveredController Info { get; }
    ControllerState State { get; }
    int? BatteryPercent { get; }
    bool CanVibrate { get; }
    bool AutoReconnect { get; set; }
    bool VerboseFrames { get; set; }

    event Action<ControllerState>? StateChanged;
    event Action<IReadOnlyList<ButtonEvent>>? ButtonEvents;
    event Action<int>? BatteryChanged;
    /// <summary>Trame brute (hexadécimale) pour l'onglet Diagnostic et la capture.</summary>
    event Action<string>? Frame;
    event Action<string>? Log;

    void Start();
    Task StopAsync();
    Task VibrateAsync(byte pattern = 0x20);
}
