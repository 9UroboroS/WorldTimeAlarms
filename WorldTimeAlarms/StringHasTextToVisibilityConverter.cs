using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WorldTimeAlarms
{
    public class StringHasTextToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
            bool hasText = value is string s && !string.IsNullOrWhiteSpace(s);

            if (invert)
                hasText = !hasText;

            return hasText ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
