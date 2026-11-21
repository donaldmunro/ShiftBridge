using ShiftBridge.Core.Obc;

namespace ShiftBridge.Platform.Obc;

/// <summary>Une sortie OpenBikeControl (BLE, TCP, DirCon). Notre appli y est le « device » (serveur).</summary>
public interface IObcTransport : IAsyncDisposable
{
    string Name { get; }

    /// <summary>Vrai si au moins une appli d'entraînement est connectée.</summary>
    bool HasClient { get; }

    /// <summary>Dernier App Info reçu (null si aucune appli ne l'a envoyé).</summary>
    ObcAppInfo? CurrentAppInfo { get; }

    event Action<ObcAppInfo>? AppInfoReceived;
    event Action<ObcHaptic>? HapticReceived;
    event Action? ClientsChanged;
    event Action<string>? Log;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();

    /// <summary>Envoie un message 0x01 déjà encodé.</summary>
    Task SendButtonStateAsync(byte[] message);

    /// <summary>Niveau de batterie de la manette (0..100), null si inconnu.</summary>
    void SetBattery(int? percent);
}
