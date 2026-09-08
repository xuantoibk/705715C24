using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EolTester.App.Converters;

/// <summary>Quy ước hiển thị ô "Phát hiện Hàng NG" ở footer — đọc từ thanh ghi SIGNAL_NG_DETECTED
/// (spec-register-map.csv): 1 = xanh (LimeGreen), 2 = cam (Orange), giá trị khác/null = xám (LightGray).</summary>
public sealed class NgValueToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        1 => Brushes.LimeGreen,
        2 => Brushes.Orange,
        _ => Brushes.LightGray,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
