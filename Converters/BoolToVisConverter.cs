using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChlorideTweaks.Converters
{
    /// <summary>Converts a bool to Visibility. Pass "Invert" to flip the result.</summary>
    public class BoolToVisConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
                flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool flag = value is Visibility v && v == Visibility.Visible;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
                flag = !flag;
            return flag;
        }
    }
}
