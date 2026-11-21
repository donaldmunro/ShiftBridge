using System.Net.NetworkInformation;
using System.Net.Sockets;
using ShiftBridge.Core.Localization;

namespace ShiftBridge.Presentation;

/// <summary>
/// Adresses de cet appareil à saisir dans une appli d'entraînement qui ne découvre pas ShiftBridge toute seule (mDNS bloqué,
/// appli sur le même appareil…).
/// </summary>
public static class LocalNetwork
{
    /// <summary>Adresses IPv4 des interfaces actives (hors boucle locale), l'adresse Wi-Fi/Ethernet en premier.</summary>
    public static IReadOnlyList<string> GetIPv4Addresses()
    {
        try
        {
            return [.. NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .OrderBy(n => n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet ? 0 : 1)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Where(a => !a.StartsWith("169.254.", StringComparison.Ordinal)) // adresse auto-attribuée : pas de réseau réel
                .Distinct()];
        }
        catch { return []; }
    }

    /// <summary>« 192.168.1.20 (autre appareil) · 127.0.0.1 (cet appareil), port 36867 » ; vide si aucun port.</summary>
    public static string Describe(IEnumerable<string> addresses, params (string Label, int Port)[] listeners)
    {
        var active = listeners.Where(l => l.Port > 0).ToList();
        if (active.Count == 0) return "";

        string hosts = string.Join(" · ", [.. addresses.Select(a => Loc.Fmt(Strings.Net_FromOtherDevice, a)), Strings.Net_ThisDevice]);
        string ports = string.Join(", ", active.Select(l => $"{l.Label} {l.Port}"));
        return Loc.Fmt(Strings.Net_Summary, hosts, ports);
    }
}
