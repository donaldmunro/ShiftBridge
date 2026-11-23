using ShiftBridge.Core.Config;

namespace ShiftBridge.Platform.Controllers;

/// <summary>Clés AXS stockées dans la configuration (hexadécimal). Le fichier de configuration contient donc un secret.</summary>
public sealed class ConfigSramKeyStore(AppConfig config) : ISramKeyStore
{
    public byte[]? Get(string serial)
    {
        lock (config.SyncRoot)
        {
            if (!config.SramAxsKeys.TryGetValue(serial, out var hex)) return null;
            try { var key = Convert.FromHexString(hex); return key.Length == 16 ? key : null; }
            catch (FormatException) { return null; }
        }
    }

    public void Set(string serial, byte[] key)
    {
        lock (config.SyncRoot) config.SramAxsKeys[serial] = Convert.ToHexString(key);
    }

    public void Remove(string serial)
    {
        lock (config.SyncRoot) config.SramAxsKeys.Remove(serial);
    }
}
