using ShiftBridge.Core.Input;
using ShiftBridge.Core.Obc;

namespace ShiftBridge.Core.Mapping;

/// <summary>Association d'une touche physique à une ou plusieurs actions OBC.</summary>
public sealed class ButtonBinding
{
    public List<ObcButtonId> Actions { get; set; } = [];

    /// <summary>Si vrai, l'action est répétée tant que la touche reste enfoncée.</summary>
    public bool RepeatOnHold { get; set; }
    public int RepeatDelayMs { get; set; } = 500;
    public int RepeatIntervalMs { get; set; } = 250;

    /// <summary>Rapport visé par l'action « Aller au rapport » (1..N) ; 0 = non réglé.</summary>
    public int GearTarget { get; set; }
}

/// <summary>Clé d'un profil d'appli : l'AppId reçu dans App Info, sans espaces ni casse (« TrainingPeaks Virtual » = « trainingpeaks virtual »).</summary>
public static class AppProfileKey
{
    public static string Normalize(string? appId) => (appId ?? "").Trim().ToLowerInvariant();
}

public sealed class Profile
{
    public string Name { get; set; } = "Par défaut";
    public Dictionary<ButtonKey, ButtonBinding> Bindings { get; set; } = [];

    public Profile Clone()
    {
        var p = new Profile { Name = Name };
        foreach (var (k, v) in Bindings)
            p.Bindings[k] = new ButtonBinding
            {
                Actions = [.. v.Actions], RepeatOnHold = v.RepeatOnHold,
                RepeatDelayMs = v.RepeatDelayMs, RepeatIntervalMs = v.RepeatIntervalMs, GearTarget = v.GearTarget,
            };
        return p;
    }

    /// <summary>
    /// Profil proche de Zwift : + / ShiftUp, − / ShiftDown, croix directionnelle en navigation.
    /// Les manettes Play et Ride partagent la même table (RightShiftUp = bouton latéral du Play droit).
    /// </summary>
    public static Profile ZwiftLike()
    {
        var p = new Profile { Name = "Défaut (type Zwift)" };
        void Map(PhysicalButton b, params ObcButtonId[] a) => p.Bindings[b] = new ButtonBinding { Actions = [.. a] };

        Map(PhysicalButton.ClickPlus, ObcButtonId.ShiftUp);
        Map(PhysicalButton.ClickMinus, ObcButtonId.ShiftDown);

        Map(PhysicalButton.RightShiftUp, ObcButtonId.ShiftUp);
        Map(PhysicalButton.RightShiftDown, ObcButtonId.ShiftDown);
        Map(PhysicalButton.LeftShiftUp, ObcButtonId.ShiftUp);
        Map(PhysicalButton.LeftShiftDown, ObcButtonId.ShiftDown);
        Map(PhysicalButton.RightPaddle, ObcButtonId.ShiftUp);
        Map(PhysicalButton.LeftPaddle, ObcButtonId.ShiftDown);

        Map(PhysicalButton.LeftUp, ObcButtonId.Up);
        Map(PhysicalButton.LeftDown, ObcButtonId.Down);
        Map(PhysicalButton.LeftLeft, ObcButtonId.Left);
        Map(PhysicalButton.LeftRight, ObcButtonId.Right);

        Map(PhysicalButton.RightA, ObcButtonId.Select);
        Map(PhysicalButton.RightB, ObcButtonId.Back);
        Map(PhysicalButton.RightY, ObcButtonId.Emote);
        Map(PhysicalButton.RightZ, ObcButtonId.CameraView);
        Map(PhysicalButton.LeftOnOff, ObcButtonId.Menu);
        Map(PhysicalButton.RightOnOff, ObcButtonId.Menu);
        Map(PhysicalButton.LeftPowerUp, ObcButtonId.PowerUp1);
        Map(PhysicalButton.RightPowerUp, ObcButtonId.PowerUp1);
        return p;
    }
}
