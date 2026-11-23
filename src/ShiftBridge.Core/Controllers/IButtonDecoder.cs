namespace ShiftBridge.Core.Controllers;

/// <summary>
/// Résultat du décodage d'une trame.
/// <paramref name="Held"/> : ensemble COMPLET des touches actuellement enfoncées (null = la trame n'en dit rien, l'état est conservé).
/// <paramref name="Taps"/> : appuis instantanés sans trame de relâchement (la manette ne signale jamais le relâchement) ; chaque élément = un appui.
/// </summary>
public sealed record DecodedFrame(IReadOnlySet<string>? Held, IReadOnlyList<string> Taps, int? BatteryPercent = null)
{
    public static readonly IReadOnlyList<string> NoTaps = [];
    public static DecodedFrame Hold(params string[] held) => new(new HashSet<string>(held), NoTaps);
    public static DecodedFrame Tap(params string[] taps) => new(null, taps);
}

/// <summary>
/// Traduit les notifications d'une manette BLE en touches. Une instance par connexion (elle peut garder un état entre deux trames) ;
/// aucune dépendance Bluetooth, donc testable avec des trames brutes.
/// </summary>
public interface IButtonDecoder
{
    /// <summary>Null si la trame n'est pas reconnue (elle est alors journalisée).</summary>
    DecodedFrame? Decode(Guid characteristic, byte[] data);
}
