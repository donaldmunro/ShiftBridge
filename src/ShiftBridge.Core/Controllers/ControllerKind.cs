namespace ShiftBridge.Core.Controllers;

/// <summary>Famille de matériel (regroupement dans l'onglet Appareils).</summary>
public enum ControllerFamily { Zwift, Groupset, Remote, SmartBike }

/// <summary>Modèle de manette BLE reconnu. Les valeurs sont enregistrées dans config.json (par nom) : ne pas renommer.</summary>
public enum ControllerKind
{
    Zwift,
    ThinkriderVs200,
    CycplusBc2,
    OnelapKey,
}

public static class ControllerKinds
{
    public static ControllerFamily Family(this ControllerKind kind) => kind switch
    {
        ControllerKind.Zwift => ControllerFamily.Zwift,
        _ => ControllerFamily.Remote,
    };

    /// <summary>Matériel pris en charge d'après des trames publiées par d'autres projets, non éprouvé ici sur du vrai matériel.</summary>
    public static bool IsBeta(this ControllerKind kind) => kind != ControllerKind.Zwift;

    /// <summary>Identifiant court et stable utilisé dans les clés de touches (« ctl:vs200:Up »).</summary>
    public static string Slug(this ControllerKind kind) => kind switch
    {
        ControllerKind.ThinkriderVs200 => "vs200",
        ControllerKind.CycplusBc2 => "bc2",
        ControllerKind.OnelapKey => "onelap",
        _ => "zwift",
    };

    public static bool TryFromSlug(string slug, out ControllerKind kind)
    {
        foreach (var k in Enum.GetValues<ControllerKind>())
            if (k != ControllerKind.Zwift && string.Equals(k.Slug(), slug, StringComparison.Ordinal)) { kind = k; return true; }
        kind = ControllerKind.Zwift;
        return false;
    }

    /// <summary>Touches (nom interne, clé de ressource du libellé) d'un modèle, dans l'ordre d'affichage. Vide pour Zwift.</summary>
    public static IReadOnlyList<(string Name, string LabelKey)> Buttons(this ControllerKind kind) => kind switch
    {
        ControllerKind.ThinkriderVs200 or ControllerKind.CycplusBc2 =>
        [
            ("Up", "Ctl_ShiftUp"), ("Down", "Ctl_ShiftDown"),
        ],
        ControllerKind.OnelapKey =>
        [
            ("Up", "Ctl_Up"), ("Down", "Ctl_Down"), ("Left", "Ctl_Left"), ("Right", "Ctl_Right"), ("Center", "Ctl_Center"),
            ("L.Up", "Ctl_LUp"), ("L.Down", "Ctl_LDown"), ("L.Left", "Ctl_LLeft"), ("L.Right", "Ctl_LRight"), ("L.Center", "Ctl_LCenter"),
            ("R.Up", "Ctl_RUp"), ("R.Down", "Ctl_RDown"), ("R.Left", "Ctl_RLeft"), ("R.Right", "Ctl_RRight"), ("R.Center", "Ctl_RCenter"),
        ],
        _ => [],
    };
}
