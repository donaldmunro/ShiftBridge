using ShiftBridge.Core.Controllers;
using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Platform.Controllers;

/// <summary>
/// Reconnaît les manettes BLE non Zwift d'après leur annonce. On se fonde sur le NOM : les services qu'elles utilisent
/// (FEA0, Nordic UART, UUID Onelap partagé avec des vélos Kettler…) sont communs à d'autres appareils.
/// </summary>
public static class ControllerCatalog
{
    public static ControllerKind? Identify(BleAdvertisement adv)
    {
        string name = (adv.LocalName ?? "").Trim();
        if (name.Length == 0) return null;
        string upper = name.ToUpperInvariant();

        // « Think VS01-0001725 » : jamais « THINK » seul, qui est aussi le préfixe des home-trainers Thinkrider (THINK X, THINK-…).
        if (upper.StartsWith("THINK VS", StringComparison.Ordinal)) return ControllerKind.ThinkriderVs200;
        // « CYCPLUS BC2 57344 ». Le service Nordic UART seul ne suffit pas (Wheeltop, L-TWOO l'utilisent aussi).
        if (upper.StartsWith("CYCPLUS", StringComparison.Ordinal) && upper.Contains("BC2", StringComparison.Ordinal)) return ControllerKind.CycplusBc2;
        // « Onelap Key_12224 ».
        if (upper == "ONELAP KEY" || upper.StartsWith("ONELAP KEY_", StringComparison.Ordinal)) return ControllerKind.OnelapKey;
        return null;
    }

    public static DiscoveredController? Detect(BleAdvertisement adv) =>
        Identify(adv) is { } kind ? new DiscoveredController(adv.Address, adv.LocalName!.Trim(), kind, adv.Rssi) : null;
}
