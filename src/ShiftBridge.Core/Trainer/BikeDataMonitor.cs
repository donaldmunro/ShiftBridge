namespace ShiftBridge.Core.Trainer;

/// <summary>
/// Surveille le flux de mesures (Indoor Bike Data) relayé à l'appli : nombre de mesures, plus long silence et dernière
/// puissance. Permet de vérifier, dans le journal, que les watts arrivent en continu même pendant une rafale de changements
/// de rapport. Thread-safe.
/// </summary>
public sealed class BikeDataMonitor
{
    private readonly object _lock = new();
    private long _lastTick = -1;
    private int _count;
    private long _maxGapMs;
    private int? _lastWatts;
    private int _powerSamples;

    public void Add(long tickMs, int? watts)
    {
        lock (_lock)
        {
            if (_lastTick >= 0) _maxGapMs = Math.Max(_maxGapMs, tickMs - _lastTick);
            _lastTick = tickMs;
            _count++;
            if (watts is { } w) { _lastWatts = w; _powerSamples++; }
        }
    }

    /// <summary>Résumé depuis le dernier appel (remis à zéro) ; null s'il n'y a eu aucune mesure.</summary>
    public string? TakeSummary(long nowMs)
    {
        lock (_lock)
        {
            if (_count == 0) return null;
            long silenceNow = _lastTick >= 0 ? nowMs - _lastTick : 0;
            long maxGap = Math.Max(_maxGapMs, silenceNow);
            string power = _powerSamples == 0 ? "pas de puissance dans les mesures" : $"dernière puissance {_lastWatts} W";
            string text = $"Mesures du trainer : {_count} relayées, plus long silence {maxGap} ms, {power}.";
            _count = 0; _maxGapMs = 0; _powerSamples = 0; // le dernier instant est conservé : le silence est mesuré d'un résumé à l'autre
            return text;
        }
    }
}
