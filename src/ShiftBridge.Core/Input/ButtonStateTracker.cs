namespace ShiftBridge.Core.Input;

public readonly record struct ButtonEvent(ButtonKey Button, bool Pressed);

/// <summary>
/// Compare chaque trame à la précédente pour produire des événements appuyé/relâché.
/// Une manette qui se déconnecte doit appeler <see cref="Reset"/> pour relâcher les touches restées enfoncées.
/// </summary>
public sealed class ButtonStateTracker
{
    private HashSet<ButtonKey> _current = [];

    public IReadOnlyCollection<ButtonKey> Current => _current;

    public List<ButtonEvent> Update(IReadOnlySet<ButtonKey> pressedNow)
    {
        var events = new List<ButtonEvent>();
        foreach (var b in _current)
            if (!pressedNow.Contains(b)) events.Add(new ButtonEvent(b, false));
        foreach (var b in pressedNow)
            if (!_current.Contains(b)) events.Add(new ButtonEvent(b, true));
        _current = [.. pressedNow];
        return events;
    }

    /// <summary>Variante pour les manettes Zwift, dont les trames décodées sont des <see cref="PhysicalButton"/>.</summary>
    public List<ButtonEvent> Update(IReadOnlySet<PhysicalButton> pressedNow) =>
        Update(pressedNow.Select(b => (ButtonKey)b).ToHashSet());

    public List<ButtonEvent> Reset() => Update(new HashSet<ButtonKey>());
}
