using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for retrieving system information and statistics
/// </summary>
public static class SystemHelper
{
    private static PerformanceCounter? _cpuCounter;
    private static long _lastBytesSent;
    private static long _lastBytesReceived;
    private static DateTime _lastNetworkSample = DateTime.MinValue;
    private static DateTime? _bootTime;
    private static bool _bootTimeFromEventLog;

    static SystemHelper()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call always returns 0
        }
        catch
        {
            _cpuCounter = null;
        }
    }

    /// <summary>
    /// Gets current CPU usage percentage (0-100)
    /// </summary>
    public static float GetCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0f;
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Gets memory information
    /// </summary>
    public static (long UsedBytes, long TotalBytes, float UsagePercent) GetMemoryInfo()
    {
        try
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                long total = (long)memStatus.ullTotalPhys;
                long available = (long)memStatus.ullAvailPhys;
                long used = total - available;
                float percent = total > 0 ? (float)used / total * 100f : 0f;
                return (used, total, percent);
            }
        }
        catch { }
        return (0, 0, 0f);
    }

    /// <summary>
    /// Gets disk usage for the system drive
    /// </summary>
    public static (long UsedBytes, long TotalBytes, float UsagePercent) GetDiskInfo(string? driveLetter = null)
    {
        try
        {
            string drive = driveLetter ?? Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var driveInfo = new DriveInfo(drive);
            if (driveInfo.IsReady)
            {
                long total = driveInfo.TotalSize;
                long free = driveInfo.AvailableFreeSpace;
                long used = total - free;
                float percent = total > 0 ? (float)used / total * 100f : 0f;
                return (used, total, percent);
            }
        }
        catch { }
        return (0, 0, 0f);
    }

    /// <summary>
    /// Gets network speed in bytes per second (upload, download)
    /// </summary>
    public static (long BytesSentPerSec, long BytesReceivedPerSec) GetNetworkSpeed()
    {
        try
        {
            long totalBytesSent = 0;
            long totalBytesReceived = 0;

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var stats = nic.GetIPStatistics();
                    totalBytesSent += stats.BytesSent;
                    totalBytesReceived += stats.BytesReceived;
                }
            }

            var now = DateTime.Now;
            var elapsed = (now - _lastNetworkSample).TotalSeconds;

            if (_lastNetworkSample == DateTime.MinValue || elapsed < 0.1)
            {
                _lastBytesSent = totalBytesSent;
                _lastBytesReceived = totalBytesReceived;
                _lastNetworkSample = now;
                return (0, 0);
            }

            long sentPerSec = (long)((totalBytesSent - _lastBytesSent) / elapsed);
            long receivedPerSec = (long)((totalBytesReceived - _lastBytesReceived) / elapsed);

            _lastBytesSent = totalBytesSent;
            _lastBytesReceived = totalBytesReceived;
            _lastNetworkSample = now;

            return (Math.Max(0, sentPerSec), Math.Max(0, receivedPerSec));
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Gets Windows version and build information
    /// </summary>
    public static (string Version, string Build, string Edition) GetWindowsInfo()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                string displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "";
                string build = key.GetValue("CurrentBuildNumber")?.ToString() ?? "";
                string ubr = key.GetValue("UBR")?.ToString() ?? "";
                string edition = key.GetValue("ProductName")?.ToString() ?? "Windows";

                // Fix: ProductName in registry shows "Windows 10" even on Windows 11
                // Build 22000+ = Windows 11, Build 26100+ = Windows 11 24H2
                if (int.TryParse(build, out int buildNum) && buildNum >= 22000 && edition.Contains("Windows 10"))
                {
                    edition = edition.Replace("Windows 10", "Windows 11");
                }

                string fullBuild = string.IsNullOrEmpty(ubr) ? build : $"{build}.{ubr}";
                return (displayVersion, fullBuild, edition);
            }
        }
        catch { }
        return ("", "", "Windows");
    }

    /// <summary>
    /// Gets the computer name
    /// </summary>
    public static string GetComputerName() => Environment.MachineName;

    /// <summary>
    /// Gets the current username
    /// </summary>
    public static string GetUserName() => Environment.UserName;

    /// <summary>
    /// Gets system uptime
    /// </summary>
    public static TimeSpan GetUptime()
    {
        if (_bootTime is null)
        {
            _bootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
        }
        return DateTime.Now - _bootTime.Value;
    }

    /// <summary>
    /// Indicates if boot time was corrected from Event Log (Fast Startup detected)
    /// </summary>
    public static bool BootTimeFromEventLog => _bootTimeFromEventLog;

    /// <summary>
    /// Loads actual boot time from Event Log (for Fast Startup correction)
    /// Call this async after UI is loaded
    /// </summary>
    public static void LoadActualBootTimeAsync()
    {
        Task.Run(() =>
        {
            try
            {
                using var log = new System.Diagnostics.Eventing.Reader.EventLogReader(
                    new System.Diagnostics.Eventing.Reader.EventLogQuery(
                        "System",
                        System.Diagnostics.Eventing.Reader.PathType.LogName,
                        "*[System[Provider[@Name='Microsoft-Windows-Kernel-Boot'] and (EventID=27)]]")
                    { ReverseDirection = true });

                var evt = log.ReadEvent();
                if (evt?.TimeCreated is not null)
                {
                    _bootTime = evt.TimeCreated.Value;
                    _bootTimeFromEventLog = true;
                }
            }
            catch { }
        });
    }

    /// <summary>
    /// Gets CPU name from WMI
    /// </summary>
    public static string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
        }
        catch { }
        return "Unknown CPU";
    }

    /// <summary>
    /// Gets total installed RAM as formatted string
    /// </summary>
    public static string GetTotalRamFormatted()
    {
        var (_, total, _) = GetMemoryInfo();
        return FormatBytes(total);
    }

    /// <summary>
    /// Formats bytes to human-readable string
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Formats bytes per second to human-readable network speed
    /// </summary>
    public static string FormatNetworkSpeed(long bytesPerSec)
    {
        if (bytesPerSec < 1024)
        {
            return $"{bytesPerSec} B/s";
        }
        if (bytesPerSec < 1024 * 1024)
        {
            return $"{bytesPerSec / 1024.0:0.#} KB/s";
        }
        return $"{bytesPerSec / 1024.0 / 1024.0:0.##} MB/s";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
