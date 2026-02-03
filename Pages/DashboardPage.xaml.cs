using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SysPilot.Controls;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Dashboard page with live system statistics
/// </summary>
public partial class DashboardPage : Page
{
    private readonly DispatcherTimer _updateTimer;

    public DashboardPage()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += async (_, _) => await UpdateStatsAsync();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadStaticInfoAsync();
        await UpdateStatsAsync();
        _updateTimer.Start();

        // Load actual boot time from Event Log in background (for Fast Startup correction)
        SystemHelper.LoadActualBootTimeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
    }

    private async Task LoadStaticInfoAsync()
    {
        // These can be slow (especially WMI calls)
        var windowsInfo = await Task.Run(() => SystemHelper.GetWindowsInfo());
        var cpuName = await Task.Run(() => SystemHelper.GetCpuName());
        var totalRam = await Task.Run(() => SystemHelper.GetTotalRamFormatted());
        var localIp = await Task.Run(() => NetworkHelper.GetLocalIpAddress());

        OsText.Text = windowsInfo.Edition;
        BuildText.Text = $"{windowsInfo.Version} (Build {windowsInfo.Build})";
        ComputerNameText.Text = SystemHelper.GetComputerName();
        UserNameText.Text = SystemHelper.GetUserName();
        CpuNameText.Text = cpuName;
        RamText.Text = totalRam;
        LocalIpText.Text = localIp;
    }

    private async Task UpdateStatsAsync()
    {
        var stats = await Task.Run(() =>
        {
            var cpu = SystemHelper.GetCpuUsage();
            var mem = SystemHelper.GetMemoryInfo();
            var disk = SystemHelper.GetDiskInfo();
            var net = SystemHelper.GetNetworkSpeed();
            var uptime = SystemHelper.GetUptime();
            return (cpu, mem, disk, net, uptime);
        });

        CpuGauge.Value = stats.cpu;
        RamGauge.Value = stats.mem.UsagePercent;
        DiskGauge.Value = stats.disk.UsagePercent;

        var totalSpeed = stats.net.BytesSentPerSec + stats.net.BytesReceivedPerSec;
        var networkPercent = Math.Min(100, totalSpeed / 1024.0 / 1024.0);
        NetworkGauge.Value = networkPercent;
        NetworkGauge.DisplayText = $"{SystemHelper.FormatNetworkSpeed(stats.net.BytesReceivedPerSec)}";

        if (SystemHelper.BootTimeFromEventLog)
        {
            UptimeText.Text = $"{stats.uptime.Days:00}d {stats.uptime.Hours:00}h {stats.uptime.Minutes:00}m";
        }
        else
        {
            UptimeText.Text = "--d --h --m";
        }
    }

    private void OpenGodMode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}");
        }
        catch
        {
            CustomDialog.Show(
                Lang.Error,
                Lang.CouldNotOpenGodMode,
                CustomDialog.DialogType.Error);
        }
    }

    private void ToggleDarkMode_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.ToggleDarkMode,
            Lang.ToggleDarkModeMessage,
            CustomDialog.DialogType.Info,
            Lang.Toggle,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            if (!RegistryHelper.ToggleDarkMode())
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotToggleDarkMode,
                    CustomDialog.DialogType.Error);
            }
        }
    }

    private void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var result = CustomDialog.Show(
            Lang.RestartExplorer,
            Lang.RestartExplorerConfirm,
            CustomDialog.DialogType.Question,
            Lang.Restart,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            ProcessHelper.RestartExplorer();
        }
    }

    private async void RestartAudio_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminHelper.RequireAdmin())
            return;

        var result = CustomDialog.Show(
            Lang.RestartAudioDriver,
            Lang.RestartAudioDriverMessage,
            CustomDialog.DialogType.Question,
            Lang.Restart,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            var success = await ServiceHelper.RestartAudioServiceAsync();
            if (!success)
            {
                CustomDialog.Show(
                    Lang.Error,
                    Lang.CouldNotRestartAudio,
                    CustomDialog.DialogType.Error);
            }
        }
    }
}
