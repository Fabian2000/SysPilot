using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for process management
/// </summary>
public static class ProcessHelper
{
    public enum ProcessCategory
    {
        App,
        Background,
        Windows
    }

    /// <summary>
    /// Base class for list items (process or group header)
    /// </summary>
    public abstract class ListItemBase
    {
        public bool IsHeader { get; protected set; }
    }

    /// <summary>
    /// Represents a group header in the process list
    /// </summary>
    public class GroupHeader : ListItemBase
    {
        public GroupHeader(ProcessCategory category, int count)
        {
            IsHeader = true;
            Category = category;
            Count = count;
            Name = category switch
            {
                ProcessCategory.App => "Apps",
                ProcessCategory.Background => "Background Processes",
                ProcessCategory.Windows => "Windows Processes",
                _ => category.ToString()
            };
        }

        public ProcessCategory Category { get; }
        public string Name { get; }
        public int Count { get; set; }
        public bool IsCollapsed { get; set; }
    }

    /// <summary>
    /// Represents process information for display
    /// </summary>
    public class ProcessInfo : ListItemBase
    {
        public ProcessInfo() { IsHeader = false; }

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long MemoryBytes { get; set; }
        public string MemoryFormatted => SystemHelper.FormatBytes(MemoryBytes);
        public double CpuPercent { get; set; }
        public string CpuFormatted => CpuPercent < 0.1 ? "0%" : $"{CpuPercent:F1}%";
        public long DiskBytesPerSec { get; set; }
        public string DiskFormatted => DiskBytesPerSec == 0 ? "0 B/s" : $"{SystemHelper.FormatBytes(DiskBytesPerSec)}/s";
        public long NetworkBytesPerSec { get; set; }
        public string NetworkFormatted => NetworkBytesPerSec == 0 ? "0 B/s" : $"{SystemHelper.FormatBytes(NetworkBytesPerSec)}/s";
        public string Status { get; set; } = "Running";
        public ImageSource? Icon { get; set; }
        public ProcessCategory Category { get; set; } = ProcessCategory.Background;
        public string WindowTitle { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    // CPU tracking
    private static readonly Dictionary<int, (long KernelTime, long UserTime, DateTime Timestamp)> _cpuTracker = new();
    private static readonly Dictionary<int, (long TotalBytes, DateTime Timestamp)> _diskTracker = new();
    private static readonly int _processorCount = Environment.ProcessorCount;

    private static readonly Dictionary<string, ImageSource?> _iconCache = new();

    private static readonly HashSet<string> WindowsProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "smss", "csrss", "wininit", "services", "lsass", "lsm",
        "svchost", "dwm", "taskhostw", "sihost", "fontdrvhost", "ctfmon",
        "RuntimeBroker", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
        "TextInputHost", "conhost", "dllhost", "WmiPrvSE", "spoolsv", "SearchIndexer",
        "SecurityHealthService", "SecurityHealthSystray", "SgrmBroker", "MsMpEng",
        "NisSrv", "audiodg", "dasHost", "WUDFHost", "CompPkgSrv", "SystemSettingsBroker",
        "ApplicationFrameHost", "UserOOBEBroker", "WidgetService", "Widgets", "LockApp",
        "SettingSyncHost", "backgroundTaskHost", "SearchProtocolHost", "SearchFilterHost",
        "MoUsoCoreWorker", "TiWorker", "TrustedInstaller", "wuauclt",
        "Memory Compression", "Idle"
    };


    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Native process enumeration
    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, int nSize);

    [DllImport("kernel32.dll")]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    private const int SystemProcessInformation = 5;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint PROCESS_VM_READ = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_PROCESS_INFORMATION
    {
        public uint NextEntryOffset;
        public uint NumberOfThreads;
        public long WorkingSetPrivateSize;
        public uint HardFaultCount;
        public uint NumberOfThreadsHighWatermark;
        public ulong CycleTime;
        public long CreateTime;
        public long UserTime;
        public long KernelTime;
        public UNICODE_STRING ImageName;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
        public uint HandleCount;
        public uint SessionId;
        public IntPtr UniqueProcessKey;
        public IntPtr PeakVirtualSize;
        public IntPtr VirtualSize;
        public uint PageFaultCount;
        public IntPtr PeakWorkingSetSize;
        public IntPtr WorkingSetSize;
        public IntPtr QuotaPeakPagedPoolUsage;
        public IntPtr QuotaPagedPoolUsage;
        public IntPtr QuotaPeakNonPagedPoolUsage;
        public IntPtr QuotaNonPagedPoolUsage;
        public IntPtr PagefileUsage;
        public IntPtr PeakPagefileUsage;
        public IntPtr PrivatePageCount;
        public long ReadOperationCount;
        public long WriteOperationCount;
        public long OtherOperationCount;
        public long ReadTransferCount;
        public long WriteTransferCount;
        public long OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    // Cache for process paths (PID -> Path), refreshed less frequently
    private static Dictionary<int, string> _pathCache = new();
    private static DateTime _lastPathCacheUpdate = DateTime.MinValue;
    private static readonly TimeSpan PathCacheExpiry = TimeSpan.FromSeconds(30);

    // Reusable buffer for NtQuerySystemInformation
    private static IntPtr _processInfoBuffer = IntPtr.Zero;
    private static int _processInfoBufferSize = 0;
    private static readonly object _bufferLock = new();

    /// <summary>
    /// Gets all running processes using native API (NtQuerySystemInformation)
    /// </summary>
    /// <param name="fullRefresh">If true, fetches paths for new processes. If false, only uses cache.</param>
    public static List<ProcessInfo> GetProcesses(bool fullRefresh = false)
    {
        var result = new List<ProcessInfo>();
        var currentIds = new HashSet<int>();

        // Get visible windows for categorization (cached for 10 seconds)
        var visiblePids = GetVisibleWindowPids();

        // Use native API to enumerate processes with reusable buffer
        lock (_bufferLock)
        {
            if (_processInfoBuffer == IntPtr.Zero)
            {
                _processInfoBufferSize = 1024 * 1024;
                _processInfoBuffer = Marshal.AllocHGlobal(_processInfoBufferSize);
            }

            int status;
            while ((status = NtQuerySystemInformation(SystemProcessInformation, _processInfoBuffer, _processInfoBufferSize, out int returnLength)) == unchecked((int)0xC0000004))
            {
                Marshal.FreeHGlobal(_processInfoBuffer);
                _processInfoBufferSize = returnLength + 65536;
                _processInfoBuffer = Marshal.AllocHGlobal(_processInfoBufferSize);
            }

            if (status != 0)
            {
                return result;
            }

            IntPtr current = _processInfoBuffer;

            while (true)
            {
                var spi = Marshal.PtrToStructure<SYSTEM_PROCESS_INFORMATION>(current);
                var pid = (int)spi.UniqueProcessId;

                if (pid > 0) // Skip Idle process
                {
                    currentIds.Add(pid);

                    string name = "";
                    if (spi.ImageName.Buffer != IntPtr.Zero && spi.ImageName.Length > 0)
                    {
                        name = Marshal.PtrToStringUni(spi.ImageName.Buffer, spi.ImageName.Length / 2) ?? "";
                    }

                    var info = new ProcessInfo
                    {
                        Id = pid,
                        Name = name,
                        MemoryBytes = (long)spi.WorkingSetSize,
                        Status = "Running"
                    };

                    // Categorize based on window visibility and known processes
                    if (WindowsProcessNames.Contains(name))
                        info.Category = ProcessCategory.Windows;
                    else if (visiblePids.Contains(pid))
                        info.Category = ProcessCategory.App;
                    else
                        info.Category = ProcessCategory.Background;

                    // Calculate CPU usage
                    var now = DateTime.UtcNow;
                    var totalTime = spi.KernelTime + spi.UserTime;
                    if (_cpuTracker.TryGetValue(pid, out var prev))
                    {
                        var timeDelta = (now - prev.Timestamp).TotalMilliseconds;
                        if (timeDelta > 0)
                        {
                            var cpuDelta = (totalTime - prev.KernelTime - prev.UserTime) / 10000.0; // Convert 100ns to ms
                            info.CpuPercent = (cpuDelta / timeDelta / _processorCount) * 100.0;
                            if (info.CpuPercent < 0)
                            {
                                info.CpuPercent = 0;
                            }
                            if (info.CpuPercent > 100)
                            {
                                info.CpuPercent = 100;
                            }
                        }
                    }
                    _cpuTracker[pid] = (spi.KernelTime, spi.UserTime, now);

                    // Disk I/O (bytes per second since last measurement)
                    var ioTotal = spi.ReadTransferCount + spi.WriteTransferCount;
                    if (_diskTracker.TryGetValue(pid, out var prevDisk))
                    {
                        var timeDelta = (now - prevDisk.Timestamp).TotalSeconds;
                        if (timeDelta > 0)
                        {
                            info.DiskBytesPerSec = (long)((ioTotal - prevDisk.TotalBytes) / timeDelta);
                            if (info.DiskBytesPerSec < 0)
                            {
                                info.DiskBytesPerSec = 0;
                            }
                        }
                    }
                    _diskTracker[pid] = (ioTotal, now);

                    // Get path and icon
                    if (_pathCache.TryGetValue(pid, out var cachedPath))
                    {
                        info.FilePath = cachedPath;
                        info.Icon = GetCachedIcon(cachedPath);
                    }
                    else if (fullRefresh)
                    {
                        // Only fetch new paths on full refresh
                        TryGetProcessPathNative(pid, info);
                        if (!string.IsNullOrEmpty(info.FilePath))
                            info.Icon = GetCachedIcon(info.FilePath);
                    }

                    result.Add(info);
                }

                if (spi.NextEntryOffset == 0)
                {
                    break;
                }

                current = IntPtr.Add(current, (int)spi.NextEntryOffset);
            }
        } // end lock

        // Clean up old cache entries periodically
        if (DateTime.Now - _lastPathCacheUpdate > PathCacheExpiry)
        {
            var toRemove = _pathCache.Keys.Where(k => !currentIds.Contains(k)).ToList();
            foreach (var id in toRemove)
                _pathCache.Remove(id);
            _lastPathCacheUpdate = DateTime.Now;
        }

        return result;
    }

    private static HashSet<int> _visiblePidsCache = new();
    private static DateTime _lastVisiblePidsUpdate = DateTime.MinValue;

    /// <summary>
    /// Gets PIDs with visible windows (cached for 10 seconds)
    /// </summary>
    private static HashSet<int> GetVisibleWindowPids()
    {
        if (DateTime.Now - _lastVisiblePidsUpdate < TimeSpan.FromSeconds(10))
            return _visiblePidsCache;

        var pids = new HashSet<int>();
        EnumWindows((hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                pids.Add((int)pid);
            }
            return true;
        }, IntPtr.Zero);

        _visiblePidsCache = pids;
        _lastVisiblePidsUpdate = DateTime.Now;
        return pids;
    }

    /// <summary>
    /// Gets process path using native API
    /// </summary>
    private static void TryGetProcessPathNative(int pid, ProcessInfo info)
    {
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
            {
                info.FilePath = sb.ToString();
                _pathCache[pid] = info.FilePath;
                info.Icon = GetCachedIcon(info.FilePath);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }


    /// <summary>
    /// Terminates a process by ID
    /// </summary>
    public static bool KillProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts Windows Explorer
    /// </summary>
    public static void RestartExplorer()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                process.Kill();
                process.Dispose();
            }

            // Explorer will auto-restart, but we can force it
            Task.Delay(500).ContinueWith(_ =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = true
                    });
                }
                catch { }
            });
        }
        catch { }
    }

    /// <summary>
    /// Opens a system tool by name
    /// </summary>
    public static bool OpenSystemTool(string toolName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = toolName,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Gets a cached icon for the given file path
    /// </summary>
    private static ImageSource? GetCachedIcon(string filePath)
    {
        lock (_iconCache)
        {
            if (_iconCache.TryGetValue(filePath, out var cached))
            {
                return cached;
            }

            var icon = ExtractIconFromFile(filePath);
            _iconCache[filePath] = icon;
            return icon;
        }
    }

    /// <summary>
    /// Extracts an icon from an executable file
    /// </summary>
    private static ImageSource? ExtractIconFromFile(string filePath)
    {
        try
        {
            var hIcon = ExtractIcon(IntPtr.Zero, filePath, 0);
            if (hIcon == IntPtr.Zero || hIcon == (IntPtr)1)
            {
                return null;
            }

            try
            {
                var icon = Icon.FromHandle(hIcon);
                var bitmap = icon.ToBitmap();

                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
