using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MusicaLibre.Converters;

public class BoolToUpDownArrowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            // Unicode arrows ↑ ↓
            return b ? "⮝" : "⮟";
        }

        return string.Empty; // fallback if not bool
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return s == "↑";
        }

        return false;
    }
}