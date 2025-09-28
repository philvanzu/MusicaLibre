namespace MusicaLibre.Converters;

using System;
using System.Globalization;
using Avalonia.Data.Converters;

public class RadioConverter : IValueConverter
{
    // value:    the bound property (e.g. SelectedOption)
    // parameter: the RadioButton's ConverterParameter (e.g. 0, 1, 2)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // If you're using numbers as parameters, make sure they're comparable
        if (value.Equals(System.Convert.ChangeType(parameter, value.GetType())))
            return true;

        return false;
    }

    // When a RadioButton is checked, update the bound property with the parameter
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isChecked = value is bool b && b;
        if (isChecked && parameter != null)
        {
            return System.Convert.ChangeType(parameter, targetType);
        }

        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
