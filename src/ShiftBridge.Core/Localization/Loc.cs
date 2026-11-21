using System.Globalization;

namespace ShiftBridge.Core.Localization;

/// <summary>
/// Accès aux textes par clé (quand la clé est calculée : table de touches, catalogue d'actions…) et formatage. Les textes à clé
/// fixe passent directement par <see cref="Strings"/>. Résolu à chaque appel : suit la culture d'interface courante.
/// </summary>
public static class Loc
{
    /// <summary>Texte de la clé dans la culture d'interface courante ; la clé elle-même si elle est inconnue.</summary>
    public static string Get(string key) =>
        Strings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>
    /// Formate un texte de ressource (<c>Strings.Clé</c>). La LANGUE du texte suit la culture d'interface, mais les nombres et
    /// pourcentages suivent les réglages régionaux du système (<see cref="CultureInfo.CurrentCulture"/>), comme le reste de
    /// l'application : un utilisateur allemand avec l'interface en anglais voit « 2,43 » partout, jamais un mélange.
    /// </summary>
    public static string Fmt(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>Choisit la forme singulier (n = 1) ou pluriel d'un texte, puis y insère n comme <c>{0}</c>.</summary>
    public static string Plural(int n, string one, string many) => Fmt(n == 1 ? one : many, n);

    /// <summary>Texte de la clé passé à <see cref="Fmt"/> (clé calculée).</summary>
    public static string Format(string key, params object?[] args) => Fmt(Get(key), args);
}
