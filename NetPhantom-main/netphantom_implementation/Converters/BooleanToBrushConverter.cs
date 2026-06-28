using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NetPhantom.Converters;

public class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36))   // red
            : new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));  // grey

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
