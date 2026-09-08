using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EolTester.App.Converters;

/// <summary>
/// So sánh giá trị enum (ToString) với ConverterParameter; trả về màu nhấn nếu khớp, màu trung tính nếu không.
/// Dùng để tô xanh nút đang ở trạng thái active trong nhóm nút độc lập (vd Auto/Manual, ON/OFF).
/// </summary>
public sealed class EnumEqualsToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Brushes.LimeGreen : Brushes.Gainsboro;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Tương tự nhưng so sánh giá trị bool trực tiếp — dùng cho cặp nút ON/OFF theo Value của một điểm I/O.
/// </summary>
public sealed class BoolEqualsToBrushConverter : IValueConverter
{
    public bool CompareTo { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b == CompareTo ? Brushes.LimeGreen : Brushes.Gainsboro;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
