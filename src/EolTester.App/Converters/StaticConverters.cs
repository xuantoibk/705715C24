using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EolTester.App.Converters;

public sealed class OnOffBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Brushes.LimeGreen : Brushes.LightGray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not true;
}

public static class StaticConverters
{
    public static readonly IValueConverter OnOffBrush = new OnOffBrushConverter();
}
