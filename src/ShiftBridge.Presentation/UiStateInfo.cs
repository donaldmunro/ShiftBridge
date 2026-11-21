using ShiftBridge.Core.Localization;

namespace ShiftBridge.Presentation;

/// <summary>
/// Ce qui accompagne la couleur d'une pastille d'état : un symbole (lisible sans distinguer les couleurs) et un texte pour les
/// lecteurs d'écran. Partagé par les deux interfaces (Windows et Android).
/// </summary>
public static class UiStateInfo
{
    public static string Glyph(UiState state) => state switch
    {
        UiState.Ok => "✓",
        UiState.Waiting => "…",
        UiState.Warning => "!",
        UiState.Error => "✕",
        _ => "○",
    };

    public static string Text(UiState state) => state switch
    {
        UiState.Ok => Strings.State_Ok,
        UiState.Waiting => Strings.State_Waiting,
        UiState.Warning => Strings.State_Warning,
        UiState.Error => Strings.State_Error,
        _ => Strings.State_Off,
    };

    /// <summary>Le disque « éteint » est gris moyen : son symbole doit être clair, alors qu'il est sombre sur les couleurs vives.</summary>
    public static bool NeedsLightGlyph(UiState state) => state is UiState.Off;
}
