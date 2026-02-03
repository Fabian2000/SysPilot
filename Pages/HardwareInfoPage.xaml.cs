using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SysPilot.Helpers;

namespace SysPilot.Pages;

/// <summary>
/// Hardware information page showing detailed system components
/// </summary>
public partial class HardwareInfoPage : Page
{
    private Storyboard? _spinnerStoryboard;

    public HardwareInfoPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void CreateSpinnerAnimation()
    {
        if (_spinnerStoryboard is not null || LoadingIcon is null)
        {
            return;
        }

        try
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1),
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(animation, LoadingIcon);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));

            _spinnerStoryboard = new Storyboard();
            _spinnerStoryboard.Children.Add(animation);
        }
        catch { }
    }

    private void ShowLoading()
    {
        CreateSpinnerAnimation();
        LoadingPanel.Visibility = Visibility.Visible;
        ContentGrid.Opacity = 0.3;
        _spinnerStoryboard?.Begin();
    }

    private void HideLoading()
    {
        _spinnerStoryboard?.Stop();
        LoadingPanel.Visibility = Visibility.Collapsed;
        ContentGrid.Opacity = 1;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        ShowLoading();

        try
        {
            var sw = Stopwatch.StartNew();
            var info = await Task.Run(() => HardwareHelper.GetHardwareInfo());
            sw.Stop();

            LoadTimeText.Text = $"({string.Format(Lang.ScannedIn, sw.ElapsedMilliseconds)})";

            DisplayCpu(info.Cpu);
            DisplayGpus(info.Gpus);
            DisplayMotherboard(info.Motherboard);
            DisplayBios(info.Bios);
            DisplayRam(info.RamModules);
            DisplayDisks(info.Disks);
            DisplayNetwork(info.NetworkAdapters);
            DisplayAudio(info.AudioDevices);
        }
        catch (Exception ex)
        {
            LoadTimeText.Text = $"{Lang.Error}: {ex.Message}";
        }

        HideLoading();
    }

    private void DisplayCpu(HardwareHelper.CpuInfo cpu)
    {
        CpuPanel.Children.Clear();
        AddInfoRow(CpuPanel, Lang.Name, cpu.Name);
        AddInfoRow(CpuPanel, Lang.CoresThreads, $"{cpu.Cores} / {cpu.Threads}");
        AddInfoRow(CpuPanel, Lang.MaxSpeed, cpu.MaxSpeed);
        AddInfoRow(CpuPanel, Lang.Socket, cpu.Socket);
        AddInfoRow(CpuPanel, Lang.L2Cache, cpu.L2Cache);
        AddInfoRow(CpuPanel, Lang.L3Cache, cpu.L3Cache);
        AddInfoRow(CpuPanel, Lang.Architecture, cpu.Architecture);
    }

    private void DisplayGpus(List<HardwareHelper.GpuInfo> gpus)
    {
        GpuPanel.Children.Clear();
        for (var i = 0; i < gpus.Count; i++)
        {
            var gpu = gpus[i];
            if (i > 0)
            {
                GpuPanel.Children.Add(new Separator
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A")),
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }
            AddInfoRow(GpuPanel, Lang.Name, gpu.Name);
            AddInfoRow(GpuPanel, Lang.VRAM, gpu.VideoMemory);
            AddInfoRow(GpuPanel, Lang.Driver, gpu.DriverVersion);
            AddInfoRow(GpuPanel, Lang.Resolution, gpu.Resolution);
            AddInfoRow(GpuPanel, Lang.RefreshRate, gpu.RefreshRate);
        }
    }

    private void DisplayMotherboard(HardwareHelper.MotherboardInfo mb)
    {
        MotherboardPanel.Children.Clear();
        AddInfoRow(MotherboardPanel, Lang.Manufacturer, mb.Manufacturer);
        AddInfoRow(MotherboardPanel, Lang.Model, mb.Product);
        AddInfoRow(MotherboardPanel, Lang.Version, mb.Version);
    }

    private void DisplayBios(HardwareHelper.BiosInfo bios)
    {
        BiosPanel.Children.Clear();
        AddInfoRow(BiosPanel, Lang.Manufacturer, bios.Manufacturer);
        AddInfoRow(BiosPanel, Lang.Version, bios.Version);
        AddInfoRow(BiosPanel, Lang.ReleaseDate, bios.ReleaseDate);
        AddInfoRow(BiosPanel, Lang.Mode, bios.Mode);
    }

    private void DisplayRam(List<HardwareHelper.RamModuleInfo> modules)
    {
        RamPanel.Children.Clear();

        // Summary
        var totalCapacity = modules.Sum(m => m.CapacityBytes);
        var speeds = modules.Select(m => m.Speed).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var summaryText = string.Format(Lang.Modules, modules.Count, SystemHelper.FormatBytes(totalCapacity));
        if (speeds.Count == 1)
            summaryText += $" @ {speeds[0]}";

        AddInfoRow(RamPanel, Lang.Total, summaryText);

        // Individual modules
        for (var i = 0; i < modules.Count; i++)
        {
            var module = modules[i];
            RamPanel.Children.Add(new Separator
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A")),
                Margin = new Thickness(0, 8, 0, 8)
            });

            var slotName = !string.IsNullOrEmpty(module.Slot) ? module.Slot : $"{Lang.Slot} {i + 1}";
            AddInfoRow(RamPanel, slotName, $"{module.Capacity} {module.Type} @ {module.Speed}");
            if (!string.IsNullOrEmpty(module.Manufacturer) && module.Manufacturer != "Unknown")
                AddInfoRow(RamPanel, Lang.Manufacturer, module.Manufacturer);
            if (!string.IsNullOrEmpty(module.PartNumber))
                AddInfoRow(RamPanel, Lang.PartNumber, module.PartNumber);
        }
    }

    private void DisplayDisks(List<HardwareHelper.DiskInfo> disks)
    {
        DiskPanel.Children.Clear();
        for (var i = 0; i < disks.Count; i++)
        {
            var disk = disks[i];
            if (i > 0)
            {
                DiskPanel.Children.Add(new Separator
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A")),
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }

            var typeColor = disk.MediaType.Contains("SSD") ? "#27AE60" : "#808080";
            AddInfoRow(DiskPanel, $"{Lang.Disk} {disk.Index}", disk.Model);
            AddInfoRow(DiskPanel, Lang.Type, disk.MediaType, typeColor);
            AddInfoRow(DiskPanel, Lang.Capacity, disk.Capacity);
            AddInfoRow(DiskPanel, Lang.Interface, disk.Interface);
        }
    }

    private void DisplayNetwork(List<HardwareHelper.NetworkAdapterInfo> adapters)
    {
        NetworkPanel.Children.Clear();
        var physicalAdapters = adapters.Where(a => a.IsPhysical).ToList();

        for (var i = 0; i < physicalAdapters.Count; i++)
        {
            var adapter = physicalAdapters[i];
            if (i > 0)
            {
                NetworkPanel.Children.Add(new Separator
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A")),
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }
            AddInfoRow(NetworkPanel, Lang.Name, adapter.Name);
            AddInfoRow(NetworkPanel, Lang.Type, adapter.ConnectionType);
            AddInfoRow(NetworkPanel, Lang.MAC, adapter.MacAddress);
            if (!string.IsNullOrEmpty(adapter.Speed))
                AddInfoRow(NetworkPanel, Lang.Speed, adapter.Speed);
        }

        if (physicalAdapters.Count == 0)
        {
            AddInfoRow(NetworkPanel, "", Lang.NoPhysicalAdapters);
        }
    }

    private void DisplayAudio(List<HardwareHelper.AudioDeviceInfo> devices)
    {
        AudioPanel.Children.Clear();
        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            if (i > 0)
            {
                AudioPanel.Children.Add(new Separator
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A")),
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }
            AddInfoRow(AudioPanel, Lang.Device, device.Name);
            if (!string.IsNullOrEmpty(device.Manufacturer))
                AddInfoRow(AudioPanel, Lang.Manufacturer, device.Manufacturer);
        }

        if (devices.Count == 0)
        {
            AddInfoRow(AudioPanel, "", Lang.NoAudioDevices);
        }
    }

    private static void AddInfoRow(StackPanel panel, string label, string value, string? valueColor = null)
    {
        if (string.IsNullOrEmpty(value)) return;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

        if (!string.IsNullOrEmpty(label))
        {
            row.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#606060")),
                FontSize = 12,
                Width = 110
            });
        }

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        var color = valueColor ?? "#E0E0E0";
        valueBlock.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));

        row.Children.Add(valueBlock);
        panel.Children.Add(row);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }
}
