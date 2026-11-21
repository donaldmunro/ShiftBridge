namespace ShiftBridge.Core.Trainer;

/// <summary>
/// Mesures de latence par étiquette (« natif », « FTMS »…) : médiane, 95e centile et nombre d'échantillons depuis le dernier
/// résumé. Sert à savoir où le temps se perd (ShiftBridge, liaison ou trainer). Thread-safe.
/// </summary>
public sealed class LatencyTracker
{
    private readonly int _window;
    private readonly object _lock = new();
    private readonly Dictionary<string, List<double>> _samples = [];

    public LatencyTracker(int window = 200) => _window = Math.Max(1, window);

    public void Add(string label, double ms)
    {
        if (ms < 0 || double.IsNaN(ms)) return;
        lock (_lock)
        {
            if (!_samples.TryGetValue(label, out var list)) _samples[label] = list = [];
            list.Add(ms);
            if (list.Count > _window) list.RemoveAt(0);
        }
    }

    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        double pos = p * (sorted.Count - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }

    /// <summary>Résumé des échantillons reçus depuis le dernier appel (les remet à zéro) ; null s'il n'y en a aucun.</summary>
    public string? TakeSummary()
    {
        List<(string Label, List<double> Values)> taken;
        lock (_lock)
        {
            taken = _samples.Where(kv => kv.Value.Count > 0).OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
            _samples.Clear();
        }
        if (taken.Count == 0) return null;

        var parts = taken.Select(t =>
        {
            t.Values.Sort();
            return $"{t.Label} médiane {Percentile(t.Values, 0.5):0} ms (p95 {Percentile(t.Values, 0.95):0}, n={t.Values.Count})";
        });
        return "Latence : " + string.Join(" ; ", parts);
    }
}
