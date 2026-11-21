using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using ShiftBridge.App.Converters;
using ShiftBridge.Presentation;

namespace ShiftBridge.App.Controls;

/// <summary>État d'un élément (manette, trainer…) : disque coloré avec un symbole, et un nom pour les lecteurs d'écran.</summary>
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(UiState), typeof(StatusBadge),
        new PropertyMetadata(UiState.Off, (d, _) => ((StatusBadge)d).Update()));

    public UiState State
    {
        get => (UiState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public StatusBadge()
    {
        InitializeComponent();
        Update();
    }

    private void Update()
    {
        var state = State;
        Disc.Background = (Brush)new UiStateToBrushConverter().Convert(state, typeof(Brush), null, CultureInfo.InvariantCulture);
        Glyph.Text = UiStateInfo.Glyph(state);
        // Symbole sombre sur les couleurs vives, clair sur le gris de l'état « inactif ».
        string key = UiStateInfo.NeedsLightGlyph(state) ? "Text" : "Bg";
        Glyph.Foreground = Application.Current?.TryFindResource(key) as Brush ?? (UiStateInfo.NeedsLightGlyph(state) ? Brushes.White : Brushes.Black);
        AutomationProperties.SetName(this, UiStateInfo.Text(state));
    }
}
