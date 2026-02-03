using System.Windows;
using System.Windows.Controls;
using SysPilot.Helpers;

namespace SysPilot.Converters;

public class ProcessListTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? ProcessTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            ProcessHelper.GroupHeader => HeaderTemplate,
            ProcessHelper.ProcessInfo => ProcessTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
