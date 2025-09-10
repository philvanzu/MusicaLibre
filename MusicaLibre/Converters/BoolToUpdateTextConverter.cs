
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace MusicaLibre.Converters
{
    public class BoolToUpdateTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMultiple)
            {
                // Optional: parameter can be a custom base text
                string baseText = parameter as string ?? "Update";
                return isMultiple ? $"Update All" : baseText;
            }

            return "Update";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
