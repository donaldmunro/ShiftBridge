using System.Windows;
using ShiftBridge.Presentation;

namespace ShiftBridge.App.Controls;

/// <summary>
/// Propriétés attachées d'une entrée de la barre latérale (style <c>NavItem</c>) : le symbole de l'icône (police Segoe Fluent Icons)
/// et, facultativement, l'état affiché par une petite pastille à droite du libellé (rien si null ou « éteint »).
/// </summary>
public static class Nav
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.RegisterAttached("Glyph", typeof(string), typeof(Nav), new PropertyMetadata(""));

    public static string GetGlyph(DependencyObject d) => (string)d.GetValue(GlyphProperty);
    public static void SetGlyph(DependencyObject d, string value) => d.SetValue(GlyphProperty, value);

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached("State", typeof(UiState?), typeof(Nav), new PropertyMetadata(null));

    public static UiState? GetState(DependencyObject d) => (UiState?)d.GetValue(StateProperty);
    public static void SetState(DependencyObject d, UiState? value) => d.SetValue(StateProperty, value);
}
