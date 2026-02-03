using System.Globalization;
using System.Windows.Data;
using SysPilot.Helpers;

namespace SysPilot.Converters;

/// <summary>
/// Converts ProcessCategory enum to display string
/// </summary>
public class CategoryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ProcessHelper.ProcessCategory.App => "Apps",
            ProcessHelper.ProcessCategory.Background => "Background Processes",
            ProcessHelper.ProcessCategory.Windows => "Windows Processes",
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
