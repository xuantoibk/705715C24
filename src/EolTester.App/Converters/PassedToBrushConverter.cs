using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EolTester.App.Converters;

public sealed class PassedToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => Brushes.LightGreen,
        false => Brushes.LightCoral,
        _ => Brushes.WhiteSmoke,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
