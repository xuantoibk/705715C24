using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using EolTester.Core.Enums;

namespace EolTester.App.Converters;

public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ConnectionState.Connected => Brushes.LimeGreen,
        ConnectionState.Connecting => Brushes.Orange,
        ConnectionState.Error => Brushes.Red,
        _ => Brushes.Gray,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
