using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ShiftBridge.Presentation;

namespace ShiftBridge.App.Converters;

/// <summary>
/// Vrai si la valeur liée est égale au paramètre (navigation par pages). Le paramètre peut lister plusieurs pages séparées par « | »
/// (« settings|diagnostic » : Réglages reste sélectionné dans la barre latérale sur la sous-page Diagnostic).
/// ConvertBack renvoie la première page de la liste quand coché.
/// </summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        parameter?.ToString()?.Split('|').Contains(value?.ToString()) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter?.ToString()?.Split('|')[0] is { } first ? first : Binding.DoNothing;
}

/// <summary>Visible si la valeur liée est égale au paramètre, sinon Collapsed.</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class BoolToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Collapsed si la valeur est null ou vide.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string ?? value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Couleur d'une pastille d'état.</summary>
public sealed class UiStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value is UiState s ? s switch
        {
            UiState.Ok => "Success",
            UiState.Warning => "Warning",
            UiState.Error => "Danger",
            UiState.Waiting => "Accent",
            _ => "Off",
        } : "Off";
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
