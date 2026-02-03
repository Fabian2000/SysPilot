using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using ScheduledTask = Microsoft.Win32.TaskScheduler.Task;
using System.Drawing;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for registry operations, primarily autostart management
/// </summary>
public static class RegistryHelper
{
    private const string CurrentUserRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LocalMachineRun = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledSuffix = "_DISABLED";

    public enum AutostartSource
    {
        Registry,
        StartupFolder,
        TaskScheduler
    }

    /// <summary>
    /// Represents an autostart entry
    /// </summary>
    public class AutostartEntry
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public bool IsEnabled { get; set; }
        public bool IsSystemWide { get; set; }
        public AutostartSource Source { get; set; } = AutostartSource.Registry;
        public string? FilePath { get; set; } // For startup folder entries
        public string? TaskPath { get; set; } // For task scheduler entries

        public string Location
        {
            get
            {
                var scope = IsSystemWide ? "System" : "User";
                return Source switch
                {
                    AutostartSource.Registry => $"Registry ({scope})",
                    AutostartSource.StartupFolder => $"Startup Folder ({scope})",
                    AutostartSource.TaskScheduler => "Task Scheduler",
                    _ => scope
                };
            }
        }

        public string SourceIcon => Source switch
        {
            AutostartSource.Registry => "Registry",
            AutostartSource.StartupFolder => "Folder",
            AutostartSource.TaskScheduler => "CalendarClock",
            _ => "Application"
        };

        public ImageSource? Icon { get; set; }
    }

    /// <summary>
    /// Gets all autostart entries from Registry, Startup folders, and Task Scheduler
    /// </summary>
    public static List<AutostartEntry> GetAutostartEntries()
    {
        var entries = new List<AutostartEntry>();

        // Registry: Current user entries
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(CurrentUserRun);
            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var value = key.GetValue(name)?.ToString() ?? "";
                    bool isDisabled = name.EndsWith(DisabledSuffix);
                    var entry = new AutostartEntry
                    {
                        Name = isDisabled ? name[..^DisabledSuffix.Length] : name,
                        Command = value,
                        IsEnabled = !isDisabled,
                        IsSystemWide = false,
                        Source = AutostartSource.Registry
                    };
                    entry.Icon = ExtractIconFromCommand(value);
                    entries.Add(entry);
                }
            }
        }
        catch { }

        // Registry: Local machine entries (system-wide)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(LocalMachineRun);
            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var value = key.GetValue(name)?.ToString() ?? "";
                    bool isDisabled = name.EndsWith(DisabledSuffix);
                    var entry = new AutostartEntry
                    {
                        Name = isDisabled ? name[..^DisabledSuffix.Length] : name,
                        Command = value,
                        IsEnabled = !isDisabled,
                        IsSystemWide = true,
                        Source = AutostartSource.Registry
                    };
                    entry.Icon = ExtractIconFromCommand(value);
                    entries.Add(entry);
                }
            }
        }
        catch { }

        // Startup Folder: User
        try
        {
            var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(userStartup))
            {
                foreach (var file in Directory.GetFiles(userStartup))
                {
                    var fileName = Path.GetFileName(file);
                    var entry = new AutostartEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Command = file,
                        FilePath = file,
                        IsEnabled = true,
                        IsSystemWide = false,
                        Source = AutostartSource.StartupFolder
                    };

                    // For shortcuts, try to get target
                    if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        var target = GetShortcutTarget(file);
                        if (!string.IsNullOrEmpty(target))
                        {
                            entry.Command = target;
                            entry.Icon = ExtractIconFromCommand(target);
                        }
                    }
                    else
                    {
                        entry.Icon = ExtractIconFromCommand(file);
                    }

                    entries.Add(entry);
                }
            }
        }
        catch { }

        // Startup Folder: Common (All Users)
        try
        {
            var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            if (Directory.Exists(commonStartup))
            {
                foreach (var file in Directory.GetFiles(commonStartup))
                {
                    var entry = new AutostartEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Command = file,
                        FilePath = file,
                        IsEnabled = true,
                        IsSystemWide = true,
                        Source = AutostartSource.StartupFolder
                    };

                    if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        var target = GetShortcutTarget(file);
                        if (!string.IsNullOrEmpty(target))
                        {
                            entry.Command = target;
                            entry.Icon = ExtractIconFromCommand(target);
                        }
                    }
                    else
                    {
                        entry.Icon = ExtractIconFromCommand(file);
                    }

                    entries.Add(entry);
                }
            }
        }
        catch { }

        // Task Scheduler: Logon tasks
        entries.AddRange(GetScheduledLogonTasks());

        return [.. entries.OrderBy(e => e.Name)];
    }

    /// <summary>
    /// Gets scheduled tasks that run at logon
    /// </summary>
    private static List<AutostartEntry> GetScheduledLogonTasks()
    {
        var tasks = new List<AutostartEntry>();
        try
        {
            using var taskService = new TaskService();
            foreach (var task in GetAllTasks(taskService.RootFolder))
            {
                try
                {
                    // Check if task has logon trigger
                    bool hasLogonTrigger = task.Definition.Triggers
                        .Any(t => t.TriggerType == TaskTriggerType.Logon);

                    if (!hasLogonTrigger)
                    {
                        continue;
                    }

                    // Get the action (usually first exec action)
                    var execAction = task.Definition.Actions
                        .OfType<ExecAction>()
                        .FirstOrDefault();

                    var entry = new AutostartEntry
                    {
                        Name = task.Name,
                        Command = execAction?.Path ?? "",
                        TaskPath = task.Path,
                        IsEnabled = task.Enabled,
                        IsSystemWide = !task.Path.StartsWith(@"\Users\", StringComparison.OrdinalIgnoreCase),
                        Source = AutostartSource.TaskScheduler
                    };

                    if (execAction is not null && !string.IsNullOrEmpty(execAction.Arguments))
                    {
                        entry.Command += " " + execAction.Arguments;
                    }

                    entry.Icon = ExtractIconFromCommand(execAction?.Path ?? "");
                    tasks.Add(entry);
                }
                catch { }
            }
        }
        catch { }
        return tasks;
    }

    private static IEnumerable<ScheduledTask> GetAllTasks(TaskFolder folder)
    {
        foreach (var task in folder.Tasks)
        {
            yield return task;
        }

        foreach (var subFolder in folder.SubFolders)
        {
            foreach (var task in GetAllTasks(subFolder))
            {
                yield return task;
            }
        }
    }

    /// <summary>
    /// Gets the target path of a shortcut (.lnk) file using Shell API
    /// </summary>
    private static string? GetShortcutTarget(string shortcutPath)
    {
        try
        {
            // Read the shortcut file and extract target path
            // .lnk files have a specific binary format
            using var fs = new FileStream(shortcutPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            // Skip header (0x4C bytes)
            fs.Position = 0x14;
            var flags = br.ReadUInt32();

            // Skip shell item id list if present
            if ((flags & 0x01) != 0)
            {
                fs.Position = 0x4C;
                var itemIdListSize = br.ReadUInt16();
                fs.Position += itemIdListSize;
            }
            else
            {
                fs.Position = 0x4C;
            }

            // Read file location info if present
            if ((flags & 0x02) != 0)
            {
                var startPos = fs.Position;
                var totalSize = br.ReadUInt32();
                var headerSize = br.ReadUInt32();
                br.ReadUInt32(); // flags
                var volumeIdOffset = br.ReadUInt32();
                var localBasePathOffset = br.ReadUInt32();

                if (localBasePathOffset > 0)
                {
                    fs.Position = startPos + localBasePathOffset;
                    var pathBytes = new List<byte>();
                    byte b;
                    while ((b = br.ReadByte()) != 0)
                    {
                        pathBytes.Add(b);
                    }
                    if (pathBytes.Count > 0)
                    {
                        return Encoding.Default.GetString([.. pathBytes]);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Enables or disables an autostart entry
    /// </summary>
    public static bool SetAutostartEnabled(AutostartEntry entry, bool enable)
    {
        return entry.Source switch
        {
            AutostartSource.Registry => SetRegistryAutostartEnabled(entry.Name, entry.IsSystemWide, enable),
            AutostartSource.StartupFolder => SetStartupFolderEnabled(entry, enable),
            AutostartSource.TaskScheduler => SetTaskSchedulerEnabled(entry, enable),
            _ => false
        };
    }

    /// <summary>
    /// Enables or disables an autostart entry by renaming it (legacy overload)
    /// </summary>
    public static bool SetAutostartEnabled(string name, bool isSystemWide, bool enable)
    {
        return SetRegistryAutostartEnabled(name, isSystemWide, enable);
    }

    private static bool SetRegistryAutostartEnabled(string name, bool isSystemWide, bool enable)
    {
        try
        {
            var rootKey = isSystemWide ? Registry.LocalMachine : Registry.CurrentUser;
            var path = isSystemWide ? LocalMachineRun : CurrentUserRun;

            using var key = rootKey.OpenSubKey(path, writable: true);
            if (key is null)
            {
                return false;
            }

            string currentName = enable ? name + DisabledSuffix : name;
            string newName = enable ? name : name + DisabledSuffix;

            var value = key.GetValue(currentName);
            if (value is null)
            {
                return false;
            }

            key.DeleteValue(currentName);
            key.SetValue(newName, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool SetStartupFolderEnabled(AutostartEntry entry, bool enable)
    {
        // Startup folder entries can't be disabled, only removed
        // We could rename the file with a .disabled extension
        if (string.IsNullOrEmpty(entry.FilePath)) return false;

        try
        {
            var path = entry.FilePath;
            var disabledPath = path + ".disabled";

            if (enable && File.Exists(disabledPath))
            {
                File.Move(disabledPath, path);
                return true;
            }
            else if (!enable && File.Exists(path))
            {
                File.Move(path, disabledPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool SetTaskSchedulerEnabled(AutostartEntry entry, bool enable)
    {
        if (string.IsNullOrEmpty(entry.TaskPath)) return false;

        try
        {
            using var taskService = new TaskService();
            var task = taskService.GetTask(entry.TaskPath);
            if (task is null)
            {
                return false;
            }

            task.Enabled = enable;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Adds a new autostart entry
    /// </summary>
    public static bool AddAutostartEntry(string name, string command, bool isSystemWide)
    {
        try
        {
            var rootKey = isSystemWide ? Registry.LocalMachine : Registry.CurrentUser;
            var path = isSystemWide ? LocalMachineRun : CurrentUserRun;

            using var key = rootKey.OpenSubKey(path, writable: true);
            if (key is null)
            {
                return false;
            }

            key.SetValue(name, command);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes an autostart entry
    /// </summary>
    public static bool RemoveAutostartEntry(AutostartEntry entry)
    {
        return entry.Source switch
        {
            AutostartSource.Registry => RemoveRegistryAutostartEntry(entry.Name, entry.IsSystemWide, entry.IsEnabled),
            AutostartSource.StartupFolder => RemoveStartupFolderEntry(entry),
            AutostartSource.TaskScheduler => false, // Don't allow deleting scheduled tasks from here
            _ => false
        };
    }

    /// <summary>
    /// Removes an autostart entry (legacy overload)
    /// </summary>
    public static bool RemoveAutostartEntry(string name, bool isSystemWide, bool isEnabled)
    {
        return RemoveRegistryAutostartEntry(name, isSystemWide, isEnabled);
    }

    private static bool RemoveRegistryAutostartEntry(string name, bool isSystemWide, bool isEnabled)
    {
        try
        {
            var rootKey = isSystemWide ? Registry.LocalMachine : Registry.CurrentUser;
            var path = isSystemWide ? LocalMachineRun : CurrentUserRun;

            using var key = rootKey.OpenSubKey(path, writable: true);
            if (key is null)
            {
                return false;
            }

            string actualName = isEnabled ? name : name + DisabledSuffix;
            key.DeleteValue(actualName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool RemoveStartupFolderEntry(AutostartEntry entry)
    {
        if (string.IsNullOrEmpty(entry.FilePath)) return false;

        try
        {
            // Also check for .disabled version
            var disabledPath = entry.FilePath + ".disabled";

            if (File.Exists(entry.FilePath))
            {
                File.Delete(entry.FilePath);
                return true;
            }
            else if (File.Exists(disabledPath))
            {
                File.Delete(disabledPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Toggles desktop icons visibility
    /// </summary>
    public static bool ToggleDesktopIcons()
    {
        try
        {
            var progman = NativeMethods.FindWindow("Progman", null);
            if (progman == nint.Zero)
            {
                return false;
            }

            var defView = NativeMethods.FindWindowEx(progman, nint.Zero, "SHELLDLL_DefView", null);
            if (defView == nint.Zero)
            {
                // Try to find in WorkerW windows
                var workerW = nint.Zero;
                do
                {
                    workerW = NativeMethods.FindWindowEx(nint.Zero, workerW, "WorkerW", null);
                    if (workerW != nint.Zero)
                    {
                        defView = NativeMethods.FindWindowEx(workerW, nint.Zero, "SHELLDLL_DefView", null);
                        if (defView != nint.Zero)
                        {
                            break;
                        }
                    }
                } while (workerW != nint.Zero);
            }

            if (defView == nint.Zero)
            {
                return false;
            }

            var listView = NativeMethods.FindWindowEx(defView, nint.Zero, "SysListView32", null);
            if (listView == nint.Zero)
            {
                return false;
            }

            bool isVisible = NativeMethods.IsWindowVisible(listView);
            NativeMethods.ShowWindow(listView, isVisible ? NativeMethods.SW_HIDE : NativeMethods.SW_SHOW);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets or sets dark mode preference
    /// </summary>
    public static bool IsDarkModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Toggles between dark and light mode
    /// </summary>
    public static bool ToggleDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: true);
            if (key is null)
            {
                return false;
            }

            bool isDark = IsDarkModeEnabled();
            int newValue = isDark ? 1 : 0;
            key.SetValue("AppsUseLightTheme", newValue, RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", newValue, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if taskbar auto-hide is enabled
    /// </summary>
    public static bool IsTaskbarAutoHideEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3");
            if (key?.GetValue("Settings") is byte[] settings && settings.Length > 8)
            {
                return (settings[8] & 0x01) != 0;
            }
        }
        catch { }
        return false;
    }

    private static readonly Dictionary<string, ImageSource?> _iconCache = new();

    /// <summary>
    /// Extracts the executable path from a command string and gets its icon
    /// </summary>
    private static ImageSource? ExtractIconFromCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        try
        {
            // Extract executable path from command
            string exePath;

            if (command.StartsWith('"'))
            {
                // Quoted path: "C:\path\to\app.exe" -args
                var endQuote = command.IndexOf('"', 1);
                if (endQuote > 1)
                {
                    exePath = command[1..endQuote];
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // Unquoted: C:\path\to\app.exe -args or just app.exe
                var spaceIndex = command.IndexOf(' ');
                exePath = spaceIndex > 0 ? command[..spaceIndex] : command;
            }

            // Expand environment variables
            exePath = Environment.ExpandEnvironmentVariables(exePath);

            if (!File.Exists(exePath))
                return null;

            lock (_iconCache)
            {
                if (_iconCache.TryGetValue(exePath, out var cached))
                    return cached;

                var icon = ExtractIconFromFile(exePath);
                _iconCache[exePath] = icon;
                return icon;
            }
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static ImageSource? ExtractIconFromFile(string filePath)
    {
        try
        {
            var hIcon = ExtractIcon(IntPtr.Zero, filePath, 0);
            if (hIcon == IntPtr.Zero || hIcon == (IntPtr)1)
                return null;

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
}
