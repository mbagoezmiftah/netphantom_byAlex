using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace NetPhantom.Converters;

public class BooleanToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? PackIconKind.CheckCircle : PackIconKind.RadioboxBlank;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
