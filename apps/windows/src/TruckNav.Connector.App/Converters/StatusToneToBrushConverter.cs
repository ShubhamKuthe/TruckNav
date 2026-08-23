using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TruckNav.Connector.App.ViewModels;

namespace TruckNav.Connector.App.Converters;

public sealed class StatusToneToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var tone = value is StatusTone statusTone ? statusTone : StatusTone.Neutral;
        var wantsBackground = string.Equals(parameter as string, "Background", StringComparison.OrdinalIgnoreCase);

        return tone switch
        {
            StatusTone.Accent => wantsBackground
                ? WithAlpha(GetBrush("AccentBrush"), 40)
                : GetBrush("AccentBrush"),
            StatusTone.Success => wantsBackground
                ? WithAlpha(GetBrush("SuccessBrush"), 36)
                : GetBrush("SuccessBrush"),
            StatusTone.Warning => wantsBackground
                ? WithAlpha(GetBrush("AccentBrush"), 36)
                : GetBrush("AccentBrush"),
            StatusTone.Error => wantsBackground
                ? WithAlpha(GetBrush("ErrorBrush"), 36)
                : GetBrush("ErrorBrush"),
            _ => wantsBackground
                ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
                : GetBrush("SecondaryTextBrush")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush GetBrush(string resourceKey) =>
        (SolidColorBrush)Application.Current.Resources[resourceKey];

    private static SolidColorBrush WithAlpha(SolidColorBrush source, byte alpha) =>
        new(Color.FromArgb(alpha, source.Color.R, source.Color.G, source.Color.B));
}
