using ShiftBridge.Core.Localization;

namespace ShiftBridge.Core.Obc;

/// <summary>IDs de boutons standard OpenBikeControl v1 (spec Revision 2026-10).</summary>
public enum ObcButtonId : byte
{
    None = 0x00,

    // Vitesses
    ShiftUp = 0x01,
    ShiftDown = 0x02,
    GearSet = 0x03,
    ChainringSet = 0x04,
    CassetteSet = 0x05,

    // Navigation
    Up = 0x10,
    Down = 0x11,
    Left = 0x12,
    Right = 0x13,
    Select = 0x14,
    Back = 0x15,
    Menu = 0x16,
    Home = 0x17,
    SteerLeft = 0x18,
    SteerRight = 0x19,
    Brake = 0x1A,
    SteeringAngle = 0x1B,

    // Social
    Emote = 0x20,
    PushToTalk = 0x21,
    Screenshot = 0x24,

    // Entraînement
    IncreaseDifficulty = 0x30,
    DecreaseDifficulty = 0x31,
    SkipInterval = 0x32,
    Pause = 0x33,
    Resume = 0x34,
    Lap = 0x35,
    PreviousInterval = 0x36,
    UTurn = 0x37,
    ChangeMode = 0x38,
    TakeABreak = 0x39,
    JoinRider = 0x3A,
    ChangeRoute = 0x3B,
    CruiseControl = 0x3C,

    // Vue
    CameraView = 0x40,
    Camera1 = 0x41,
    Camera2 = 0x42,
    Camera3 = 0x43,
    HudToggle = 0x44,
    MapToggle = 0x45,
    SpectateRider = 0x46,

    // Power-ups
    PowerUp1 = 0x50,
    PowerUp2 = 0x51,
    PowerUp3 = 0x52,

    // Actions INTERNES à ShiftBridge (hors plage de la spec OBC) : elles pilotent la vitesse virtuelle et ne sont jamais
    // envoyées à l'appli d'entraînement.
    CenterGear = 0xF0,
    GoToGear = 0xF1,
    ShiftUpBy = 0xF2,
    ShiftDownBy = 0xF3,
}

public static class ObcButtonCatalog
{
    /// <summary>Boutons proposés à l'affectation et clé de leur catégorie. Les textes viennent des ressources (<c>Cat_*</c>, <c>Action_&lt;Id&gt;</c>).</summary>
    private static readonly (ObcButtonId Id, string CategoryKey)[] Table =
    [
        (ObcButtonId.None, "Cat_None"),
        (ObcButtonId.ShiftUp, "Cat_Gears"),
        (ObcButtonId.ShiftDown, "Cat_Gears"),
        (ObcButtonId.CenterGear, "Cat_Gears"),
        (ObcButtonId.GoToGear, "Cat_Gears"),
        (ObcButtonId.ShiftUpBy, "Cat_Gears"),
        (ObcButtonId.ShiftDownBy, "Cat_Gears"),
        (ObcButtonId.Up, "Cat_Navigation"),
        (ObcButtonId.Down, "Cat_Navigation"),
        (ObcButtonId.Left, "Cat_Navigation"),
        (ObcButtonId.Right, "Cat_Navigation"),
        (ObcButtonId.Select, "Cat_Navigation"),
        (ObcButtonId.Back, "Cat_Navigation"),
        (ObcButtonId.Menu, "Cat_Navigation"),
        (ObcButtonId.Home, "Cat_Navigation"),
        (ObcButtonId.SteerLeft, "Cat_Navigation"),
        (ObcButtonId.SteerRight, "Cat_Navigation"),
        (ObcButtonId.Emote, "Cat_Social"),
        (ObcButtonId.PushToTalk, "Cat_Social"),
        (ObcButtonId.Screenshot, "Cat_Social"),
        (ObcButtonId.IncreaseDifficulty, "Cat_Training"),
        (ObcButtonId.DecreaseDifficulty, "Cat_Training"),
        (ObcButtonId.SkipInterval, "Cat_Training"),
        (ObcButtonId.PreviousInterval, "Cat_Training"),
        (ObcButtonId.Pause, "Cat_Training"),
        (ObcButtonId.Resume, "Cat_Training"),
        (ObcButtonId.Lap, "Cat_Training"),
        (ObcButtonId.UTurn, "Cat_Training"),
        (ObcButtonId.ChangeMode, "Cat_Training"),
        (ObcButtonId.CameraView, "Cat_View"),
        (ObcButtonId.HudToggle, "Cat_View"),
        (ObcButtonId.MapToggle, "Cat_View"),
        (ObcButtonId.PowerUp1, "Cat_PowerUps"),
        (ObcButtonId.PowerUp2, "Cat_PowerUps"),
        (ObcButtonId.PowerUp3, "Cat_PowerUps"),
    ];

    /// <summary>Vrai pour une action propre à ShiftBridge (vitesse centrale, aller au rapport…) : jamais envoyée à l'appli, jamais filtrée par App Info.</summary>
    public static bool IsInternal(ObcButtonId id) => id is ObcButtonId.CenterGear or ObcButtonId.GoToGear or ObcButtonId.ShiftUpBy or ObcButtonId.ShiftDownBy;

    /// <summary>Libellés et catégories pour l'interface, dans la langue courante. Seuls les boutons numériques (appui/relâche) sont proposés.</summary>
    public static IReadOnlyList<(ObcButtonId Id, string Category, string Label)> Assignable =>
        [.. Table.Select(t => (t.Id, Loc.Get(t.CategoryKey), Loc.Get($"Action_{t.Id}")))];
}
