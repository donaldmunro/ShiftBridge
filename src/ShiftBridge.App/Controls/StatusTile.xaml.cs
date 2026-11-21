using System.Windows;
using System.Windows.Controls;
using ShiftBridge.Presentation;

namespace ShiftBridge.App.Controls;

/// <summary>Contenu d'une tuile de l'Accueil (état, rubrique, résumé, détail), à placer dans un bouton de style <c>Tile</c>.</summary>
public partial class StatusTile : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(UiState), typeof(StatusTile), new PropertyMetadata(UiState.Off));
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatusTile), new PropertyMetadata(""));
    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(string), typeof(StatusTile), new PropertyMetadata(""));
    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(string), typeof(StatusTile), new PropertyMetadata(""));

    public UiState State { get => (UiState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Summary { get => (string)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public string Detail { get => (string)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public StatusTile() => InitializeComponent();
}
