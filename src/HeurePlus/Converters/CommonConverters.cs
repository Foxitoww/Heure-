using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HeurePlus.Converters;

/// <summary>bool -&gt; Visibility. ConverterParameter="invert" pour inverser.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Inverse un booléen.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>null / chaîne vide -&gt; Collapsed, sinon Visible.</summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool empty = value is null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
            empty = !empty;
        return empty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Compare la valeur à ConverterParameter (utile pour des "radios" d'enum).</summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Binding.DoNothing : Binding.DoNothing;
}

/// <summary>
/// Clé de statut ("Travail", "Conge", "None"…) -&gt; Brush trouvé dans les ressources
/// (clé "Status.Travail" etc.). Fallback : "Status.None".
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = "Status." + (value?.ToString() ?? "None");
        return Application.Current.TryFindResource(key)
               ?? Application.Current.TryFindResource("Status.None")
               ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Index sélectionné -&gt; Visible si égal à ConverterParameter, sinon Collapsed.
/// Sert à afficher la bonne page selon l'élément actif de la barre de navigation.
/// </summary>
public sealed class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int current = value is int i ? i : 0;
        int target = int.TryParse(parameter?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) ? t : -1;
        return current == target ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Multiplie une valeur numérique par ConverterParameter (double).</summary>
public sealed class MultiplyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        double v = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        double factor = parameter is null
            ? 1
            : double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture);
        return v * factor;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
