namespace ShiftBridge.Platform.Trainer;

/// <summary>
/// UNE connexion à un trainer, quel que soit le transport (DirCon/Wi-Fi, Bluetooth FTMS, Bluetooth FE-C). Elle expose le
/// trainer comme un arbre GATT. Les propriétés des caractéristiques sont au format DirCon (Read 0x01, Write 0x02,
/// Notify 0x04, Indicate 0x08). Aucune logique de reconnexion ici : voir <see cref="ReconnectingTrainerLink"/>.
/// </summary>
public interface ITrainerLink : IAsyncDisposable
{
    bool IsConnected { get; }

    /// <summary>Notification ou indication reçue du trainer : caractéristique + valeur.</summary>
    event Action<Guid, byte[]>? Notification;
    event Action? Disconnected;
    event Action<string>? Log;

    Task<List<Guid>> DiscoverServicesAsync();
    Task<List<(Guid Uuid, byte Props)>> DiscoverCharacteristicsAsync(Guid service);
    Task<byte[]> ReadAsync(Guid characteristic);
    Task WriteAsync(Guid characteristic, byte[] value);
    Task EnableNotificationsAsync(Guid characteristic, bool enable = true);
}

/// <summary>Liaison qui se reconnecte seule après une coupure : le proxy y rejoue l'état de contrôle de l'appli.</summary>
public interface IReconnectingLink : ITrainerLink
{
    /// <summary>Levé quand la liaison est de nouveau utilisable (abonnements rétablis).</summary>
    event Action? Reconnected;
    /// <summary>Levé à chaque coupure détectée (la reconnexion est alors en cours).</summary>
    event Action? Dropped;
}
