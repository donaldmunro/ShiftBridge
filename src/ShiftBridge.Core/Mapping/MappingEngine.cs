using ShiftBridge.Core.Input;
using ShiftBridge.Core.Obc;

namespace ShiftBridge.Core.Mapping;

/// <summary>
/// Traduit les événements physiques en changements d'état OBC (0 relâché, 1 appuyé).
/// Un compteur par action évite qu'un relâchement de touche coupe une action encore tenue par une autre touche.
/// Les actions que l'appli cible n'a pas déclarées (App Info) sont filtrées.
/// </summary>
public sealed class MappingEngine
{
    private readonly Dictionary<ObcButtonId, int> _held = [];
    private readonly Dictionary<ButtonKey, List<ObcButtonId>> _activePerButton = [];

    public Profile Profile { get; set; } = Profile.ZwiftLike();

    /// <summary>Boutons supportés par l'appli cible. Null ou vide = tous (convention de la spec).</summary>
    public IReadOnlySet<ObcButtonId>? SupportedActions { get; set; }

    /// <summary>
    /// Actions jamais filtrées par <see cref="SupportedActions"/>. Avec le proxy trainer actif, ShiftBridge applique lui-même
    /// les vitesses : une appli qui ne déclare pas ShiftUp/ShiftDown ne doit pas empêcher le virtual shifting.
    /// </summary>
    public IReadOnlySet<ObcButtonId> AlwaysAllowed { get; set; } = new HashSet<ObcButtonId>();

    public List<(ObcButtonId Id, byte State)> Translate(IEnumerable<ButtonEvent> events)
    {
        var changes = new List<(ObcButtonId, byte)>();
        foreach (var e in events)
        {
            if (e.Pressed)
            {
                if (!Profile.Bindings.TryGetValue(e.Button, out var binding)) continue;
                var actions = binding.Actions
                    .Where(a => a != ObcButtonId.None && IsSupported(a))
                    .Distinct().ToList();
                _activePerButton[e.Button] = actions;
                foreach (var a in actions)
                {
                    _held[a] = _held.GetValueOrDefault(a) + 1;
                    if (_held[a] == 1) changes.Add((a, 1));
                }
            }
            else if (_activePerButton.Remove(e.Button, out var actions))
            {
                // On relâche ce qui a été pressé, même si le profil a changé entre-temps.
                foreach (var a in actions)
                {
                    int n = _held.GetValueOrDefault(a) - 1;
                    if (n <= 0) { _held.Remove(a); changes.Add((a, 0)); }
                    else _held[a] = n;
                }
            }
        }
        return changes;
    }

    private bool IsSupported(ObcButtonId id) =>
        ObcButtonCatalog.IsInternal(id) || // propres à ShiftBridge : l'appli n'a rien à déclarer
        AlwaysAllowed.Contains(id) || SupportedActions is null || SupportedActions.Count == 0 || SupportedActions.Contains(id);
}
