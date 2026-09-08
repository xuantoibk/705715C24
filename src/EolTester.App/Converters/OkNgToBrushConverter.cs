using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EolTester.App.Converters;

/// <summary>Quy ước OK/NG dùng chung ở tab Main: OK (true) = nền trắng, NG (false) = nền cam, null (chưa
/// xác định) = trong suốt.</summary>
public sealed class OkNgToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => Brushes.White,
        false => Brushes.Orange,
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
