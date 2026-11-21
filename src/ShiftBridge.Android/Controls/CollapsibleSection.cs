namespace ShiftBridge.Mobile;

/// <summary>
/// Section repliée par défaut (« › Avancé ») qui déroule son contenu au toucher : l'équivalent mobile de l'Expander de la version
/// Windows. Le contenu XAML placé entre les balises devient le corps de la section.
/// </summary>
[ContentProperty(nameof(Body))]
public sealed class CollapsibleSection : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(CollapsibleSection), "", propertyChanged: (b, _, _) => ((CollapsibleSection)b).Refresh());
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded), typeof(bool), typeof(CollapsibleSection), false, propertyChanged: (b, _, _) => ((CollapsibleSection)b).Refresh());

    private readonly Label _chevron;
    private readonly Label _title;
    private readonly ContentView _host;

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    public View? Body { get => _host.Content; set => _host.Content = value; }

    public CollapsibleSection()
    {
        var accent = EqualsToColorConverter.Resource("Accent");
        _chevron = new Label { FontFamily = Icons.Font, FontSize = 22, TextColor = accent, VerticalOptions = LayoutOptions.Center };
        _title = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = accent, VerticalOptions = LayoutOptions.Center };
        var header = new HorizontalStackLayout
        {
            Spacing = 6, Padding = new Thickness(0, 10), MinimumHeightRequest = 44, HorizontalOptions = LayoutOptions.Start,
            Children = { _chevron, _title },
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => IsExpanded = !IsExpanded;
        header.GestureRecognizers.Add(tap);

        _host = new ContentView { IsVisible = false };
        Content = new VerticalStackLayout { Spacing = 0, Children = { header, _host } };
        Refresh();
    }

    private void Refresh()
    {
        _chevron.Text = IsExpanded ? Icons.Expanded : Icons.Collapsed;
        _title.Text = Title;
        _host.IsVisible = IsExpanded;
        SemanticProperties.SetDescription(this, Title);
    }
}
