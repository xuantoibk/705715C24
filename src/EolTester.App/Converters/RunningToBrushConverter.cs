using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EolTester.App.Converters;

public sealed class RunningToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Brushes.IndianRed : Brushes.LightGreen;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
