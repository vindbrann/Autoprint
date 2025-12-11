using System;
using System.Globalization;
using System.Windows.Data;

namespace Autoprint.Client.Converters
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InvertBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue;
            }
            return value;
        }
    }
}