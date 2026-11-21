using System.Globalization;

namespace ShiftBridge.Core.Localization;

/// <summary>
/// Choix de la langue de l'interface : français si la langue du système est francophone (fr-FR, fr-BE, fr-CH, fr-CA…),
/// anglais sinon. Une préférence explicite (« fr » ou « en ») l'emporte. Seule la culture d'interface (UI) change : les formats
/// de nombres et de dates continuent de suivre le système.
/// </summary>
public static class AppLanguage
{
    public const string Auto = "auto";
    public const string French = "fr";
    public const string English = "en";

    /// <summary>
    /// Culture d'interface du système, relevée au premier accès (donc avant que <see cref="Apply(string?)"/> ne la remplace) :
    /// un second appel à <see cref="Apply(string?)"/> repart ainsi toujours de la vraie langue du système.
    /// </summary>
    public static CultureInfo SystemUiCulture { get; } = CultureInfo.CurrentUICulture;

    /// <summary>Langue effectivement appliquée à l'interface (« fr » ou « en »).</summary>
    public static string ActiveLanguage { get; private set; } = Resolve(null, CultureInfo.CurrentUICulture);

    /// <summary>Ramène une préférence quelconque (config éditée à la main…) à « auto », « fr » ou « en ».</summary>
    public static string Normalize(string? preference) => preference?.Trim().ToLowerInvariant() switch
    {
        French => French,
        English => English,
        _ => Auto,
    };

    /// <summary>Langue effective ("fr" ou "en") pour une préférence et la culture d'interface du système.</summary>
    public static string Resolve(string? preference, CultureInfo systemUiCulture) => Normalize(preference) switch
    {
        French => French,
        English => English,
        _ => systemUiCulture.TwoLetterISOLanguageName == "fr" ? French : English,
    };

    /// <summary>
    /// Applique la langue : à appeler au démarrage, avant de créer fenêtres et ViewModels. Pour le français sur un système
    /// francophone, la culture régionale (fr-BE…) est conservée.
    /// </summary>
    public static CultureInfo Apply(string? preference) => Apply(preference, SystemUiCulture);

    /// <summary>Comme <see cref="Apply(string?)"/> avec une langue système imposée.</summary>
    public static CultureInfo Apply(string? preference, CultureInfo systemUiCulture)
    {
        var culture = Choose(preference, systemUiCulture);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        ActiveLanguage = Resolve(preference, systemUiCulture);
        return culture;
    }

    /// <summary>Culture d'interface à appliquer, sans rien modifier (fr-BE reste fr-BE ; le reste est « fr » ou « en »).</summary>
    public static CultureInfo Choose(string? preference, CultureInfo systemUiCulture) =>
        Resolve(preference, systemUiCulture) == French
            ? (systemUiCulture.TwoLetterISOLanguageName == "fr" ? systemUiCulture : CultureInfo.GetCultureInfo("fr"))
            : CultureInfo.GetCultureInfo("en");
}
