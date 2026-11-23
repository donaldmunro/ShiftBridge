namespace ShiftBridge.Core.Controllers;

/// <summary>UUID Bluetooth communs aux décodeurs.</summary>
public static class BleUuids
{
    /// <summary>UUID 16 bits étendu sur la base Bluetooth (0000xxxx-0000-1000-8000-00805F9B34FB).</summary>
    public static Guid Uuid16(ushort id) => new($"0000{id:x4}-0000-1000-8000-00805f9b34fb");

    /// <summary>Nordic UART Service : service et caractéristique TX (notifications de la manette vers l'hôte).</summary>
    public static readonly Guid NordicUartService = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    public static readonly Guid NordicUartTx = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");
    /// <summary>Caractéristique RX du Nordic UART : celle où l'hôte écrit.</summary>
    public static readonly Guid NordicUartRx = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
}
