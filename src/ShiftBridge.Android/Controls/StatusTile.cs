using ShiftBridge.Presentation;

namespace ShiftBridge.Mobile;

/// <summary>Contenu d'une tuile de l'Accueil : pastille d'état, rubrique, résumé en gras, détail discret et chevron « ouvrir ».</summary>
public sealed class StatusTile : ContentView
{
    public static readonly BindableProperty StateProperty = BindableProperty.Create(nameof(State), typeof(UiState), typeof(StatusTile), UiState.Off,
        propertyChanged: (b, _, n) => ((StatusTile)b)._badge.State = (UiState)n);
    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(nameof(Heading), typeof(string), typeof(StatusTile), "",
        propertyChanged: (b, _, n) => ((StatusTile)b)._heading.Text = (string)n);
    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(nameof(Summary), typeof(string), typeof(StatusTile), "",
        propertyChanged: (b, _, n) => ((StatusTile)b)._summary.Text = (string)n);
    public static readonly BindableProperty DetailProperty = BindableProperty.Create(nameof(Detail), typeof(string), typeof(StatusTile), "",
        propertyChanged: (b, _, n) => { var t = (StatusTile)b; t._detail.Text = (string)n; t._detail.IsVisible = !string.IsNullOrEmpty((string)n); });

    private readonly StatusBadge _badge = new();
    private readonly Label _heading, _summary, _detail;

    public UiState State { get => (UiState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public string Heading { get => (string)GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string Summary { get => (string)GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
    public string Detail { get => (string)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public StatusTile()
    {
        var muted = EqualsToColorConverter.Resource("TextMuted");
        _heading = new Label { FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = muted, VerticalOptions = LayoutOptions.Center };
        _summary = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
        _detail = new Label { FontSize = 12, TextColor = muted, IsVisible = false };
        var chevron = new Label
        {
            Text = Icons.Chevron, FontFamily = Icons.Font, FontSize = 22, TextColor = muted,
            VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End,
        };
        var header = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        header.Add(_badge, 0);
        header.Add(_heading, 1);
        header.Add(chevron, 2);
        Content = new VerticalStackLayout { Spacing = 4, Children = { header, _summary, _detail } };
    }
}
