namespace ShiftBridge.Presentation;

/// <summary>
/// Clés des pages (valeurs de <see cref="MainViewModel.CurrentPage"/>) et leur hiérarchie : Réglages et Appareils sont à un clic de
/// l'Accueil, Diagnostic est un niveau plus bas (Accueil → Réglages → Diagnostic). Partagé par les interfaces Windows et Android.
/// </summary>
public static class Pages
{
    public const string Home = "home";
    public const string Devices = "devices";
    public const string Buttons = "buttons";
    public const string Trainer = "trainer";
    public const string Apps = "apps";
    public const string Settings = "settings";
    public const string Diagnostic = "diagnostic";

    public static readonly IReadOnlyList<string> All = [Home, Devices, Buttons, Trainer, Apps, Settings, Diagnostic];

    /// <summary>Page vers laquelle « retour » ramène ; null depuis l'Accueil (là, le bouton retour quitte l'application).</summary>
    public static string? ParentOf(string? page) => page switch
    {
        Home => null,
        Diagnostic => Settings,
        _ => Home,
    };
}
