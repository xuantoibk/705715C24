using System.Globalization;
using System.Windows.Data;

namespace EolTester.App.Converters;

/// <summary>Quy ước OK/NG dùng chung ở tab Main: OK (true) = "OK", NG (false) = "NG", null (chưa xác định)
/// = <see cref="Placeholder"/> (rỗng cho cột "OK / NG", "--" khi thay thế cột "Giá trị" số).</summary>
public sealed class OkNgToTextConverter : IValueConverter
{
    public string Placeholder { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => "OK",
        false => "NG",
        _ => Placeholder,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
