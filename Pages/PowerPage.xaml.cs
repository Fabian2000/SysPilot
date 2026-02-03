using System.Windows;
using System.Windows.Controls;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Power options and system tools page
/// </summary>
public partial class PowerPage : Page
{
    public PowerPage()
    {
        InitializeComponent();
    }

    private void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.Shutdown,
            Lang.ShutdownConfirm,
            CustomDialog.DialogType.Warning,
            Lang.Shutdown,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (!PowerHelper.Shutdown())
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotShutdown,
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void ForceShutdown_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.ForceShutdown,
            Lang.ForceShutdownConfirm,
            CustomDialog.DialogType.Warning,
            Lang.ForceShutdown,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (!PowerHelper.ForceShutdown())
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotForceShutdown,
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.Restart,
            Lang.RestartConfirm,
            CustomDialog.DialogType.Warning,
            Lang.Restart,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (!PowerHelper.Restart())
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotRestart,
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void ForceRestart_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.ForceRestart,
            Lang.ForceRestartConfirm,
            CustomDialog.DialogType.Warning,
            Lang.ForceRestart,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (!PowerHelper.ForceRestart())
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotForceRestart,
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        if (!PowerHelper.Sleep())
        {
            CustomDialog.Show(
                Lang.Error,
                Lang.CouldNotSleep,
                CustomDialog.DialogType.Error);
        }
    }

    private void Hibernate_Click(object sender, RoutedEventArgs e)
    {
        if (!PowerHelper.Hibernate())
        {
            CustomDialog.Show(
                Lang.Error,
                Lang.CouldNotHibernate,
                CustomDialog.DialogType.Error);
        }
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        PowerHelper.Lock();
    }

    private void Logoff_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.LogOff,
            Lang.LogOffConfirm,
            CustomDialog.DialogType.Warning,
            Lang.LogOff,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (!PowerHelper.Logoff())
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotLogOff,
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void OpenTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string toolName)
        {
            if (!ProcessHelper.OpenSystemTool(toolName))
            {
                CustomDialog.Show(
                    Lang.Error,
                    string.Format(Lang.CouldNotOpenTool, toolName),
                    CustomDialog.DialogType.Error);
            }
        }
    }
}
