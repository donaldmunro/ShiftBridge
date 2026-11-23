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
    ShimanoDi2,
    SramAxs,
    WahooKickrBike,
    EliteSquare,
    StagesSb20,
    CycplusT7h,
    WheeltopEds,
    Ltwoo,
}

public static class ControllerKinds
{
    public static ControllerFamily Family(this ControllerKind kind) => kind switch
    {
        ControllerKind.Zwift => ControllerFamily.Zwift,
        ControllerKind.ShimanoDi2 or ControllerKind.SramAxs or ControllerKind.WheeltopEds or ControllerKind.Ltwoo => ControllerFamily.Groupset,
        ControllerKind.WahooKickrBike or ControllerKind.StagesSb20 or ControllerKind.EliteSquare => ControllerFamily.SmartBike,
        _ => ControllerFamily.Remote,
    };

    /// <summary>Matériel pris en charge d'après des trames publiées par d'autres projets, non éprouvé ici sur du vrai matériel.</summary>
    public static bool IsBeta(this ControllerKind kind) => kind != ControllerKind.Zwift;

    /// <summary>
    /// Appareil qui n'est QUE une télécommande ou un groupe : il ne doit jamais figurer dans la liste des trainers. Un vélo connecté
    /// (KICKR Bike, Stages SB20) est aussi un trainer : il reste proposé comme tel.
    /// </summary>
    public static bool IsPureRemote(this ControllerKind kind) =>
        kind is not (ControllerKind.Zwift or ControllerKind.WahooKickrBike or ControllerKind.StagesSb20);

    /// <summary>Identifiant court et stable utilisé dans les clés de touches (« ctl:vs200:Up »).</summary>
    public static string Slug(this ControllerKind kind) => kind switch
    {
        ControllerKind.ThinkriderVs200 => "vs200",
        ControllerKind.CycplusBc2 => "bc2",
        ControllerKind.OnelapKey => "onelap",
        ControllerKind.ShimanoDi2 => "di2",
        ControllerKind.SramAxs => "axs",
        ControllerKind.WahooKickrBike => "kickr",
        ControllerKind.EliteSquare => "square",
        ControllerKind.StagesSb20 => "sb20",
        ControllerKind.CycplusT7h => "t7h",
        ControllerKind.WheeltopEds => "wheeltop",
        ControllerKind.Ltwoo => "ltwoo",
        _ => "zwift",
    };

    public static bool TryFromSlug(string slug, out ControllerKind kind)
    {
        foreach (var k in Enum.GetValues<ControllerKind>())
            if (k != ControllerKind.Zwift && string.Equals(k.Slug(), slug, StringComparison.Ordinal)) { kind = k; return true; }
        kind = ControllerKind.Zwift;
        return false;
    }

    private static (string Name, string LabelKey)[] Named(ControllerKind kind, params string[] names) =>
        [.. names.Select(n => (n, $"Ctl_{kind.Slug()}_{n.Replace(".", "")}"))];

    private static string[] Units(string[] prefixes, params string[] names) =>
        [.. prefixes.SelectMany(p => names.Select(n => p + n))];

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
        ControllerKind.ShimanoDi2 => Named(kind, "Ch1", "Ch2", "Ch3", "Ch4"),
        ControllerKind.SramAxs => Named(kind, "Single", "Double"),
        ControllerKind.WahooKickrBike => Named(kind, "RightUp", "RightDown", "RightSteer", "LeftUp", "LeftDown", "LeftSteer",
            "ShiftUpRight", "ShiftDownRight", "ShiftUpLeft", "ShiftDownLeft", "RightBrake", "LeftBrake"),
        ControllerKind.EliteSquare => Named(kind, "Up", "Down", "Left", "Right", "X", "Square", "LeftShift1", "LeftShift2", "LeftBrake", "CampyLeft",
            "Y", "A", "B", "Z", "Circle", "Triangle", "RightShift1", "RightShift2", "RightBrake", "CampyRight"),
        ControllerKind.StagesSb20 => Named(kind, "LeftUp", "LeftDown", "LeftThird", "RightUp", "RightDown", "RightThird"),
        ControllerKind.CycplusT7h => Named(kind, Units(["L.", "R."], "ShiftUp", "ShiftDown", "RearArrow", "FrontArrow")),
        ControllerKind.WheeltopEds => Named(kind, Units(["", "L.", "R.", "F."], "Up", "Down", "FineUp", "FineDown")),
        ControllerKind.Ltwoo => Named(kind, "Up", "Down", "Front"),
        _ => [],
    };
}
