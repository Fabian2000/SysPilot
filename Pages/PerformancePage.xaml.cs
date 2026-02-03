using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SysPilot.Helpers;

namespace SysPilot.Pages;

public partial class PerformancePage : Page
{
    private readonly DispatcherTimer _timer;
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskCounter;
    private PerformanceCounter? _diskReadCounter;
    private PerformanceCounter? _diskWriteCounter;

    private long _lastBytesSent;
    private long _lastBytesReceived;
    private DateTime _lastNetworkSample = DateTime.MinValue;

    private List<DiskInfo> _disks = [];
    private int _selectedDiskIndex;

    public class DiskInfo
    {
        public string Name { get; set; } = "";
        public string InstanceName { get; set; } = ""; // For performance counter
        public long Capacity { get; set; }
        public string DriveLetter { get; set; } = "";
    }

    public PerformancePage()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load disks first
        _disks = await Task.Run(GetDisks);
        DiskSelector.ItemsSource = _disks;
        if (_disks.Count > 0)
        {
            DiskSelector.SelectedIndex = 0;
        }

        // Initialize counters in background
        await Task.Run(InitializeCounters);

        // Load static info in background, then update UI
        var info = await Task.Run(GetStaticInfo);
        ApplyStaticInfo(info);

        _timer.Start();
        Timer_Tick(null, EventArgs.Empty); // Initial update
    }

    private List<DiskInfo> GetDisks()
    {
        var disks = new List<DiskInfo>();
        try
        {
            // Get physical disks
            using var searcher = new ManagementObjectSearcher("SELECT Index, Model, Size FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var index = Convert.ToInt32(obj["Index"]);
                var model = obj["Model"]?.ToString() ?? $"Disk {index}";
                var size = Convert.ToInt64(obj["Size"] ?? 0);

                disks.Add(new DiskInfo
                {
                    Name = $"Disk {index} ({model})",
                    InstanceName = index.ToString(),
                    Capacity = size
                });
            }
        }
        catch { }

        return [.. disks.OrderBy(d => d.InstanceName)];
    }

    private void DiskSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DiskSelector.SelectedIndex < 0 || DiskSelector.SelectedIndex >= _disks.Count)
        {
            return;
        }

        _selectedDiskIndex = DiskSelector.SelectedIndex;
        var disk = _disks[_selectedDiskIndex];

        // Recreate disk counters for selected disk
        try
        {
            _diskCounter?.Dispose();
            _diskReadCounter?.Dispose();
            _diskWriteCounter?.Dispose();

            var instanceName = $"{disk.InstanceName} _Total";
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", instanceName);
            _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instanceName);
            _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instanceName);
            _diskCounter.NextValue();
            _diskReadCounter.NextValue();
            _diskWriteCounter.NextValue();

            DiskCapacityText.Text = SystemHelper.FormatBytes(disk.Capacity);
        }
        catch { }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        DisposeCounters();
    }

    private void InitializeCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call returns 0
        }
        catch { }

        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            _diskCounter.NextValue();
            _diskReadCounter.NextValue();
            _diskWriteCounter.NextValue();
        }
        catch { }

        // Initialize network baseline
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();

            if (interfaces.Count > 0)
            {
                var stats = interfaces[0].GetIPStatistics();
                _lastBytesSent = stats.BytesSent;
                _lastBytesReceived = stats.BytesReceived;
                _lastNetworkSample = DateTime.UtcNow;
            }
        }
        catch { }
    }

    private void DisposeCounters()
    {
        _cpuCounter?.Dispose();
        _diskCounter?.Dispose();
        _diskReadCounter?.Dispose();
        _diskWriteCounter?.Dispose();
    }

    private record StaticSystemInfo(
        string CpuName, int CpuCores, int CpuThreads, string CpuBaseSpeed,
        string TotalRam, string RamSpeed, string RamSlots,
        string NetworkName, string NetworkAdapter);

    private StaticSystemInfo GetStaticInfo()
    {
        string cpuName = "", cpuBaseSpeed = "";
        int cpuCores = 0, cpuThreads = 0;
        string totalRam = "", ramSpeed = "", ramSlots = "";
        string networkName = "", networkAdapter = "";

        // CPU Info
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                cpuName = obj["Name"]?.ToString() ?? "";
                cpuName = cpuName.Replace("Intel(R) Core(TM)", "Intel")
                                 .Replace("AMD Ryzen", "Ryzen")
                                 .Replace(" CPU", "")
                                 .Replace("  ", " ")
                                 .Trim();
                cpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                cpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                var maxSpeed = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                cpuBaseSpeed = $"{maxSpeed / 1000.0:0.00} GHz";
                break;
            }
        }
        catch { }

        // RAM Info
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            totalRam = SystemHelper.FormatBytes(gcInfo.TotalAvailableMemoryBytes);

            // Get RAM speed and slot count
            int usedSlots = 0;
            int speed = 0;
            using var memSearcher = new ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory");
            foreach (var obj in memSearcher.Get())
            {
                usedSlots++;
                var s = Convert.ToInt32(obj["Speed"] ?? 0);
                if (s > speed)
                {
                    speed = s;
                }
            }
            ramSpeed = speed > 0 ? $"{speed} MHz" : "--";
            ramSlots = $"{usedSlots} used";
        }
        catch { }

        // Network adapter name
        try
        {
            var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            if (activeInterface is not null)
            {
                networkName = activeInterface.Name;
                networkAdapter = activeInterface.NetworkInterfaceType.ToString();
            }
        }
        catch { }

        return new StaticSystemInfo(cpuName, cpuCores, cpuThreads, cpuBaseSpeed, totalRam, ramSpeed, ramSlots, networkName, networkAdapter);
    }

    private void ApplyStaticInfo(StaticSystemInfo info)
    {
        CpuNameText.Text = info.CpuName;
        RamTotalText.Text = info.TotalRam;
        NetworkNameText.Text = info.NetworkName;
        NetAdapterText.Text = info.NetworkAdapter;

        // Update CPU details
        CpuCoresText.Text = info.CpuCores.ToString();
        CpuLogicalText.Text = info.CpuThreads.ToString();
        CpuSpeedText.Text = info.CpuBaseSpeed;

        // Update RAM details
        MemSpeedText.Text = info.RamSpeed;
        MemSlotsText.Text = info.RamSlots;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateCpu();
        UpdateMemory();
        UpdateDisk();
        UpdateNetwork();
    }

    private void UpdateCpu()
    {
        try
        {
            var cpuPercent = _cpuCounter?.NextValue() ?? 0;
            CpuGraph.AddValue(cpuPercent);
            CpuUtilText.Text = $"{cpuPercent:0.0}%";

            // Process count
            var processes = Process.GetProcesses();
            CpuProcessesText.Text = processes.Length.ToString();
        }
        catch { }
    }

    private void UpdateMemory()
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var totalBytes = gcInfo.TotalAvailableMemoryBytes;

            // Get actual memory info via performance counter or WMI
            using var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var freeKb = Convert.ToInt64(obj["FreePhysicalMemory"]);
                var totalKb = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                var usedKb = totalKb - freeKb;

                var usedPercent = (double)usedKb / totalKb * 100;
                MemoryGraph.AddValue(usedPercent);

                MemInUseText.Text = SystemHelper.FormatBytes(usedKb * 1024);
                MemAvailText.Text = SystemHelper.FormatBytes(freeKb * 1024);
                break;
            }
        }
        catch { }
    }

    private void UpdateDisk()
    {
        try
        {
            var diskPercent = _diskCounter?.NextValue() ?? 0;
            diskPercent = Math.Min(diskPercent, 100); // Can exceed 100% sometimes
            DiskGraph.AddValue(diskPercent);
            DiskActiveText.Text = $"{diskPercent:0.0}%";

            var readBytes = _diskReadCounter?.NextValue() ?? 0;
            var writeBytes = _diskWriteCounter?.NextValue() ?? 0;

            DiskReadText.Text = FormatSpeed(readBytes);
            DiskWriteText.Text = FormatSpeed(writeBytes);
        }
        catch { }
    }

    private void UpdateNetwork()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();

            if (interfaces.Count == 0)
            {
                return;
            }

            long totalSent = 0, totalReceived = 0;
            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPStatistics();
                totalSent += stats.BytesSent;
                totalReceived += stats.BytesReceived;
            }

            var now = DateTime.UtcNow;
            var elapsed = (now - _lastNetworkSample).TotalSeconds;

            if (elapsed > 0 && _lastNetworkSample != DateTime.MinValue)
            {
                var sendBps = (totalSent - _lastBytesSent) / elapsed;
                var recvBps = (totalReceived - _lastBytesReceived) / elapsed;

                var sendKbps = sendBps * 8 / 1000; // Convert to Kbps
                var recvKbps = recvBps * 8 / 1000;

                // Use total throughput for graph (scaled to show activity)
                var totalKbps = sendKbps + recvKbps;
                var graphValue = Math.Min(totalKbps / 10, 100); // Scale: 1000 Kbps = 100%
                NetworkGraph.AddValue(graphValue);

                NetSendText.Text = FormatNetworkSpeed(sendBps);
                NetRecvText.Text = FormatNetworkSpeed(recvBps);
            }

            _lastBytesSent = totalSent;
            _lastBytesReceived = totalReceived;
            _lastNetworkSample = now;
        }
        catch { }
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec < 1024)
        {
            return $"{bytesPerSec:0} B/s";
        }
        if (bytesPerSec < 1024 * 1024)
        {
            return $"{bytesPerSec / 1024:0.0} KB/s";
        }
        return $"{bytesPerSec / 1024 / 1024:0.0} MB/s";
    }

    private static string FormatNetworkSpeed(double bytesPerSec)
    {
        var bitsPerSec = bytesPerSec * 8;
        if (bitsPerSec < 1000)
        {
            return $"{bitsPerSec:0} bps";
        }
        if (bitsPerSec < 1000000)
        {
            return $"{bitsPerSec / 1000:0.0} Kbps";
        }
        return $"{bitsPerSec / 1000000:0.0} Mbps";
    }
}
