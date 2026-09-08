using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EolTester.App.Converters;

/// <summary>Chuỗi null/rỗng → Collapsed — dùng cho các TextBlock hint/lỗi chỉ nên chiếm chỗ khi thực sự có nội dung
/// (VD cảnh báo vượt 125 thanh ghi, thông báo lỗi validate), tránh để lại khoảng trống cố định khi rỗng.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
