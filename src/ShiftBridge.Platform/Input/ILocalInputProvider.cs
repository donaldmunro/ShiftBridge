using ShiftBridge.Core.Input;

namespace ShiftBridge.Platform.Input;

public enum LocalInputKind { Keyboard, Gamepad, Joystick }

/// <summary>Périphérique local détecté (clavier, manette Xbox, joystick générique).</summary>
public sealed record LocalInputDevice(string Id, string Name, LocalInputKind Kind, bool CanVibrate);

/// <summary>
/// Source d'entrée locale à la machine (clavier, télécommandes HID, manettes de jeu), par opposition aux manettes Zwift en BLE.
/// Les événements arrivent déjà fusionnés : un bouton est « appuyé » dès qu'au moins un périphérique le tient.
/// Les événements peuvent être levés depuis n'importe quel thread.
/// </summary>
public interface ILocalInputProvider : IAsyncDisposable
{
    IReadOnlyList<LocalInputDevice> Devices { get; }
    event Action? DevicesChanged;
    event Action<IReadOnlyList<ButtonEvent>>? ButtonEvents;
    event Action<string>? Log;

    /// <summary>Active ou coupe l'écoute du clavier et des manettes de jeu.</summary>
    void Configure(bool keyboard, bool gamepads);

    /// <summary>
    /// Touches clavier « kbd:* » à écouter (celles qui ont une action). Les autres ne sont ni lues ni journalisées.
    /// Avec <paramref name="suppress"/>, ces touches sont en plus avalées : elles n'atteignent pas l'appli au premier plan.
    /// </summary>
    void SetBoundKeys(IReadOnlySet<ButtonKey> keys, bool suppress);

    /// <summary>
    /// Mode « appuie sur une touche » : le prochain appui (clavier ou manette) est avalé et signalé par <see cref="Captured"/>
    /// au lieu d'être émis comme événement de bouton. Se désactive tout seul après la capture.
    /// </summary>
    bool CaptureNext { get; set; }
    event Action<ButtonKey>? Captured;

    /// <summary>Vrai si au moins une manette connectée sait vibrer.</summary>
    bool CanVibrate { get; }
    Task VibrateAsync();
}

/// <summary>Aucune source locale (Android, tests, plateformes sans support).</summary>
public sealed class NullLocalInputProvider : ILocalInputProvider
{
    public IReadOnlyList<LocalInputDevice> Devices => [];
    public event Action? DevicesChanged { add { } remove { } }
    public event Action<IReadOnlyList<ButtonEvent>>? ButtonEvents { add { } remove { } }
    public event Action<string>? Log { add { } remove { } }
    public event Action<ButtonKey>? Captured { add { } remove { } }
    public bool CaptureNext { get; set; }
    public bool CanVibrate => false;
    public void Configure(bool keyboard, bool gamepads) { }
    public void SetBoundKeys(IReadOnlySet<ButtonKey> keys, bool suppress) { }
    public Task VibrateAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
