using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using ShiftBridge.Presentation;

namespace ShiftBridge.Mobile;

/// <summary>État d'un élément (manette, trainer…) : disque coloré avec un symbole, et une description pour TalkBack.</summary>
public sealed class StatusBadge : ContentView
{
    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(UiState), typeof(StatusBadge), UiState.Off,
        propertyChanged: (b, _, _) => ((StatusBadge)b).Update());

    private readonly Border _disc;
    private readonly Label _glyph;

    public UiState State
    {
        get => (UiState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public StatusBadge()
    {
        _glyph = new Label
        {
            FontSize = 12, FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
        };
        _disc = new Border
        {
            WidthRequest = 22, HeightRequest = 22, StrokeThickness = 0, Padding = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start,
            Content = _glyph,
        };
        Content = _disc;
        Update();
    }

    private void Update()
    {
        var state = State;
        _disc.BackgroundColor = (Color)new UiStateToColorConverter().Convert(state, typeof(Color), null, CultureInfo.InvariantCulture)!;
        _glyph.Text = UiStateInfo.Glyph(state);
        // Symbole sombre sur les couleurs vives, clair sur le gris de l'état « inactif ».
        _glyph.TextColor = EqualsToColorConverter.Resource(UiStateInfo.NeedsLightGlyph(state) ? "Text" : "Bg");
        SemanticProperties.SetDescription(this, UiStateInfo.Text(state));
    }
}
