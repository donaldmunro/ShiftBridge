namespace ShiftBridge.Core.Mapping;

/// <summary>Vitesse de répétition d'une vitesse + / − tenue (réglage global).</summary>
public enum RepeatSpeed { Slow, Normal, Fast, VeryFast }

public static class RepeatSpeeds
{
    public const RepeatSpeed Default = RepeatSpeed.Fast;

    /// <summary>Attente avant la première répétition, puis intervalle entre deux répétitions.</summary>
    public static (int DelayMs, int IntervalMs) Timings(RepeatSpeed speed) => speed switch
    {
        RepeatSpeed.Slow => (500, 250),
        RepeatSpeed.Normal => (400, 180),
        RepeatSpeed.VeryFast => (200, 80),
        _ => (300, 120),
    };
}
