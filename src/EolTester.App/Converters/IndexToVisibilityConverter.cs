using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EolTester.App.Converters;

public sealed class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int current && parameter is string s && int.TryParse(s, out var target))
        {
            return current == target ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
