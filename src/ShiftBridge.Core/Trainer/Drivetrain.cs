namespace ShiftBridge.Core.Trainer;

/// <summary>
/// Braquet physique du vélo sur le trainer : dents du plateau et du pignon (avec un Zwift Cog : 14 dents, 18 sur les Elite).
/// Le rapport réel (plateau ÷ pignon) sert de référence au virtual shifting : k = rapport virtuel ÷ rapport réel.
/// Zwift recommande le petit plateau et un pignon bas ; 34×14 donne 2,43, très proche du rapport 12 de la table Zwift (2,40).
/// </summary>
public readonly record struct Drivetrain(int ChainringTeeth, int CogTeeth)
{
    public const int MinChainring = 20, MaxChainring = 70, MinCog = 9, MaxCog = 52;

    public static Drivetrain Default => new(34, 14);

    public double Ratio => CogTeeth > 0 ? (double)ChainringTeeth / CogTeeth : 0;

    public bool IsValid => ChainringTeeth is >= MinChainring and <= MaxChainring && CogTeeth is >= MinCog and <= MaxCog;

    /// <summary>Plateau le plus proche donnant ce rapport avec ce pignon (pour proposer un braquet d'après une mesure).</summary>
    public static int ChainringFor(double ratio, int cogTeeth) =>
        Math.Clamp((int)Math.Round(ratio * cogTeeth), MinChainring, MaxChainring);

    public override string ToString() => $"{ChainringTeeth}×{CogTeeth}";
}
