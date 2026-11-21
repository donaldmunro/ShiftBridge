using System.Globalization;
using ShiftBridge.Presentation;

namespace ShiftBridge.Mobile;

/// <summary>Vrai si la valeur liée (texte) est égale au paramètre (ou à l'une des valeurs séparées par « | ») : sert à afficher une seule page à la fois.</summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Matches(value, parameter);

    internal static bool Matches(object? value, object? parameter) =>
        parameter?.ToString()?.Split('|').Contains(value?.ToString()) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Accent si la valeur est égale au paramètre, sinon gris : met en valeur l'onglet courant.</summary>
public sealed class EqualsToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        EqualsToBoolConverter.Matches(value, parameter) ? Resource("Accent") : Resource("TextMuted");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    internal static Color Resource(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c ? c : Colors.Gray;
}

/// <summary>Couleur d'une pastille d'état.</summary>
public sealed class UiStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        EqualsToColorConverter.Resource(value is UiState s ? s switch
        {
            UiState.Ok => "Success",
            UiState.Warning => "Warning",
            UiState.Error => "Danger",
            UiState.Waiting => "Accent",
            _ => "Off",
        } : "Off");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Vrai si le texte lié n'est ni null ni vide.</summary>
public sealed class HasTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !string.IsNullOrEmpty(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
