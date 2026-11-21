using ShiftBridge.Core.Localization;

namespace ShiftBridge.Platform.Trainer;

/// <summary>Résultat d'une mesure du rapport réel (plateau ÷ pignon) à partir de la vitesse et de la cadence du trainer.</summary>
public sealed record RatioMeasurement(bool Success, double Ratio, int Samples, double Spread, string Message)
{
    public static RatioMeasurement Ok(double ratio, int samples, double spread) =>
        new(true, ratio, samples, spread, Loc.Fmt(Strings.Measure_Ok, ratio, samples, spread));

    public static RatioMeasurement Failure(string message) => new(false, 0, 0, 1, message);
}
