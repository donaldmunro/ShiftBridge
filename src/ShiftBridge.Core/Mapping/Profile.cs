using ShiftBridge.Core.Controllers;
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

    /// <summary>Nombre de vitesses des actions « Monter / Descendre de X vitesses » (1..23) ; 0 = non réglé.</summary>
    public int GearSteps { get; set; }
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
                GearSteps = v.GearSteps,
            };
        return p;
    }

    /// <summary>Assignations proposées à la première connexion d'un modèle de manette BLE (haut = vitesse +, bas = vitesse −).</summary>
    public static IReadOnlyList<(string Name, ObcButtonId Action)> ControllerDefaults(ControllerKind kind) => kind switch
    {
        ControllerKind.ThinkriderVs200 or ControllerKind.CycplusBc2 =>
            [("Up", ObcButtonId.ShiftUp), ("Down", ObcButtonId.ShiftDown)],
        ControllerKind.OnelapKey =>
            [("Up", ObcButtonId.ShiftUp), ("Down", ObcButtonId.ShiftDown), ("Left", ObcButtonId.Left), ("Right", ObcButtonId.Right), ("Center", ObcButtonId.Select)],
        _ => [],
    };

    /// <summary>Ajoute les assignations par défaut d'un modèle, sans toucher à celles qui existent déjà. Vrai si le profil a changé.</summary>
    public bool ApplyControllerDefaults(ControllerKind kind)
    {
        bool changed = false;
        foreach (var (name, action) in ControllerDefaults(kind))
        {
            var key = ButtonKey.Controller(kind, name);
            if (Bindings.ContainsKey(key)) continue;
            Bindings[key] = new ButtonBinding { Actions = [action] };
            changed = true;
        }
        return changed;
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
        Map(PhysicalButton.LeftShiftUp, ObcButtonId.ShiftDown); // Click v2 gauche : gauche = −, droite = + (convention BikeControl)
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
