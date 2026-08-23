using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TruckNav.Connector.App.Converters;

public sealed class BooleanToSelectionBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isSelected = value as bool? == true;
        return isSelected
            ? new SolidColorBrush(Color.FromRgb(34, 43, 53))
            : Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
