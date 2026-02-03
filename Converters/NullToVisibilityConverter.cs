using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SysPilot.Converters;

/// <summary>
/// Converts null to Visible and non-null to Collapsed
/// </summary>
public class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
