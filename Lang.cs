#nullable enable
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SysPilot;

public static class Lang
{
    private static Dictionary<string, string>? _strings;
    private static Dictionary<string, string>? _fallback;
    private static string _currentLang = "";

    private static string LangFolder => Path.Combine(AppContext.BaseDirectory, "lang");

    public static string CurrentLanguage => _currentLang;

    public static void Load(string? language = null)
    {
        // Check for preferred language override file
        var preferredLangFile = Path.Combine(LangFolder, ".preferred-lang");
        if (language is null && File.Exists(preferredLangFile))
        {
            var preferred = File.ReadAllText(preferredLangFile).Trim();
            if (!string.IsNullOrEmpty(preferred))
            {
                language = preferred;
            }
        }

        var lang = language ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        if (_strings is not null && _currentLang == lang)
        {
            return;
        }

        _currentLang = lang;

        // Load fallback (English)
        var enPath = Path.Combine(LangFolder, "en.json");
        if (File.Exists(enPath))
        {
            _fallback = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath));
        }

        // Load requested language
        var langPath = Path.Combine(LangFolder, $"{lang}.json");
        if (File.Exists(langPath))
        {
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(langPath));
        }
        else
        {
            _strings = _fallback;
        }

        _fallback ??= new();
        _strings ??= _fallback;
    }

    private static string Get(string key)
    {
        if (_strings is null)
        {
            Load();
        }

        if (_strings!.TryGetValue(key, out var value))
        {
            return value;
        }
        if (_fallback!.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }
        return key;
    }

    // Navigation
    public static string AppTitle => Get("AppTitle");
    public static string NavDashboard => Get("NavDashboard");
    public static string NavPerformance => Get("NavPerformance");
    public static string NavTasks => Get("NavTasks");
    public static string NavAutostart => Get("NavAutostart");
    public static string NavServices => Get("NavServices");
    public static string NavNetwork => Get("NavNetwork");
    public static string NavCleanup => Get("NavCleanup");
    public static string NavPrograms => Get("NavPrograms");
    public static string NavHardware => Get("NavHardware");
    public static string NavPower => Get("NavPower");

    // Dashboard
    public static string DashboardTitle => Get("DashboardTitle");
    public static string SystemOverview => Get("SystemOverview");
    public static string CPU => Get("CPU");
    public static string Memory => Get("Memory");
    public static string Disk => Get("Disk");
    public static string Network => Get("Network");
    public static string SystemInfo => Get("SystemInfo");
    public static string ComputerName => Get("ComputerName");
    public static string UserName => Get("UserName");
    public static string WindowsVersion => Get("WindowsVersion");
    public static string Uptime => Get("Uptime");
    public static string QuickActions => Get("QuickActions");
    public static string EmptyRecycleBin => Get("EmptyRecycleBin");
    public static string FlushDNS => Get("FlushDNS");
    public static string RestartExplorer => Get("RestartExplorer");
    public static string TempCleanup => Get("TempCleanup");
    public static string SystemStatus => Get("SystemStatus");
    public static string GodMode => Get("GodMode");
    public static string DarkMode => Get("DarkMode");
    public static string AudioDriver => Get("AudioDriver");
    public static string OperatingSystem => Get("OperatingSystem");
    public static string Build => Get("Build");
    public static string User => Get("User");
    public static string InstalledRAM => Get("InstalledRAM");
    public static string LocalIP => Get("LocalIP");
    public static string Hardware => Get("Hardware");

    // Performance
    public static string PerformanceTitle => Get("PerformanceTitle");
    public static string PerformanceSubtitle => Get("PerformanceSubtitle");
    public static string CPUUsage => Get("CPUUsage");
    public static string MemoryUsage => Get("MemoryUsage");
    public static string DiskActivity => Get("DiskActivity");
    public static string NetworkActivity => Get("NetworkActivity");
    public static string Processes => Get("Processes");
    public static string Threads => Get("Threads");
    public static string Handles => Get("Handles");
    public static string Used => Get("Used");
    public static string Available => Get("Available");
    public static string Read => Get("Read");
    public static string Write => Get("Write");
    public static string Sent => Get("Sent");
    public static string Received => Get("Received");
    public static string RealTimeMonitoring => Get("RealTimeMonitoring");
    public static string Utilization => Get("Utilization");
    public static string Logical => Get("Logical");
    public static string InUse => Get("InUse");
    public static string Slots => Get("Slots");
    public static string ActiveTime => Get("ActiveTime");
    public static string Send => Get("Send");
    public static string Receive => Get("Receive");
    public static string Adapter => Get("Adapter");

    // Task Manager
    public static string TaskManagerTitle => Get("TaskManagerTitle");
    public static string TaskManagerSubtitle => Get("TaskManagerSubtitle");
    public static string TaskManager => Get("TaskManager");
    public static string SearchProcesses => Get("SearchProcesses");
    public static string EndTask => Get("EndTask");
    public static string EndTasks => Get("EndTasks");
    public static string EndTaskConfirm => Get("EndTaskConfirm");
    public static string EndTasksConfirm => Get("EndTasksConfirm");
    public static string UnsavedDataWarning => Get("UnsavedDataWarning");
    public static string EndTaskError => Get("EndTaskError");
    public static string EndTasksError => Get("EndTasksError");
    public static string ProcessEndedOrElevated => Get("ProcessEndedOrElevated");
    public static string Refresh => Get("Refresh");
    public static string ProcessName => Get("ProcessName");
    public static string ProcessCPU => Get("ProcessCPU");
    public static string ProcessMemory => Get("ProcessMemory");
    public static string ProcessDisk => Get("ProcessDisk");
    public static string LoadingProcesses => Get("LoadingProcesses");
    public static string Details => Get("Details");
    public static string OpenFileLocation => Get("OpenFileLocation");
    public static string CopyPath => Get("CopyPath");
    public static string PickWindow => Get("PickWindow");

    // Autostart
    public static string AutostartTitle => Get("AutostartTitle");
    public static string AutostartSubtitle => Get("AutostartSubtitle");
    public static string SearchAutostart => Get("SearchAutostart");
    public static string Enable => Get("Enable");
    public static string Disable => Get("Disable");
    public static string Delete => Get("Delete");
    public static string AddEntry => Get("AddEntry");
    public static string SourceRegistry => Get("SourceRegistry");
    public static string SourceTaskScheduler => Get("SourceTaskScheduler");
    public static string SourceStartupFolder => Get("SourceStartupFolder");
    public static string LoadingAutostart => Get("LoadingAutostart");

    // Services
    public static string ServicesTitle => Get("ServicesTitle");
    public static string ServicesSubtitle => Get("ServicesSubtitle");
    public static string SearchServices => Get("SearchServices");
    public static string Start => Get("Start");
    public static string Stop => Get("Stop");
    public static string Restart => Get("Restart");
    public static string ServiceRunning => Get("ServiceRunning");
    public static string ServiceStopped => Get("ServiceStopped");
    public static string ServiceStarting => Get("ServiceStarting");
    public static string ServiceStopping => Get("ServiceStopping");
    public static string LoadingServices => Get("LoadingServices");

    // Network
    public static string NetworkTitle => Get("NetworkTitle");
    public static string NetworkSubtitle => Get("NetworkSubtitle");
    public static string ActiveConnections => Get("ActiveConnections");
    public static string NetworkAdapters => Get("NetworkAdapters");
    public static string LocalAddress => Get("LocalAddress");
    public static string RemoteAddress => Get("RemoteAddress");
    public static string State => Get("State");
    public static string Protocol => Get("Protocol");
    public static string IPAddress => Get("IPAddress");
    public static string MACAddress => Get("MACAddress");
    public static string Speed => Get("Speed");
    public static string Status => Get("Status");
    public static string Adapters => Get("Adapters");
    public static string Connections => Get("Connections");
    public static string PublicIP => Get("PublicIP");
    public static string Process => Get("Process");
    public static string LocalPort => Get("LocalPort");
    public static string RemotePort => Get("RemotePort");

    // Cleanup
    public static string CleanupTitle => Get("CleanupTitle");
    public static string CleanupSubtitle => Get("CleanupSubtitle");
    public static string CleanupDescription => Get("CleanupDescription");
    public static string Analyze => Get("Analyze");
    public static string CleanNow => Get("CleanNow");
    public static string SelectAll => Get("SelectAll");
    public static string DeselectAll => Get("DeselectAll");
    public static string TotalSelected => Get("TotalSelected");
    public static string WindowsTemp => Get("WindowsTemp");
    public static string UserTemp => Get("UserTemp");
    public static string RecycleBin => Get("RecycleBin");
    public static string WindowsUpdateCache => Get("WindowsUpdateCache");
    public static string ThumbnailCache => Get("ThumbnailCache");
    public static string Analyzing => Get("Analyzing");
    public static string Cleaning => Get("Cleaning");
    public static string CleanupComplete => Get("CleanupComplete");
    public static string SpaceFreed => Get("SpaceFreed");
    public static string Files => Get("Files");
    public static string Scan => Get("Scan");
    public static string SelectNone => Get("SelectNone");
    public static string Clean => Get("Clean");
    public static string Scanning => Get("Scanning");

    // Programs
    public static string ProgramsTitle => Get("ProgramsTitle");
    public static string ProgramsSubtitle => Get("ProgramsSubtitle");
    public static string SearchPrograms => Get("SearchPrograms");
    public static string ShowSystemComponents => Get("ShowSystemComponents");
    public static string Uninstall => Get("Uninstall");
    public static string Programs => Get("Programs");
    public static string LoadingPrograms => Get("LoadingPrograms");
    public static string Publisher => Get("Publisher");
    public static string Version => Get("Version");
    public static string Size => Get("Size");
    public static string InstallDate => Get("InstallDate");

    // Hardware
    public static string HardwareTitle => Get("HardwareTitle");
    public static string HardwareSubtitle => Get("HardwareSubtitle");
    public static string Processor => Get("Processor");
    public static string Graphics => Get("Graphics");
    public static string Motherboard => Get("Motherboard");
    public static string BIOS => Get("BIOS");
    public static string MemoryModules => Get("MemoryModules");
    public static string StorageDevices => Get("StorageDevices");
    public static string NetworkAdaptersHW => Get("NetworkAdaptersHW");
    public static string AudioDevices => Get("AudioDevices");
    public static string ScanningHardware => Get("ScanningHardware");
    public static string ScannedIn => Get("ScannedIn");
    public static string CoresThreads => Get("CoresThreads");
    public static string MaxSpeed => Get("MaxSpeed");
    public static string Socket => Get("Socket");
    public static string L2Cache => Get("L2Cache");
    public static string L3Cache => Get("L3Cache");
    public static string Architecture => Get("Architecture");
    public static string Resolution => Get("Resolution");
    public static string RefreshRate => Get("RefreshRate");
    public static string ReleaseDate => Get("ReleaseDate");
    public static string Mode => Get("Mode");
    public static string Modules => Get("Modules");
    public static string Slot => Get("Slot");
    public static string PartNumber => Get("PartNumber");
    public static string Interface => Get("Interface");
    public static string MAC => Get("MAC");
    public static string Device => Get("Device");
    public static string NoPhysicalAdapters => Get("NoPhysicalAdapters");
    public static string NoAudioDevices => Get("NoAudioDevices");
    public static string Cores => Get("Cores");
    public static string BaseSpeed => Get("BaseSpeed");
    public static string VRAM => Get("VRAM");
    public static string Driver => Get("Driver");
    public static string Manufacturer => Get("Manufacturer");
    public static string Model => Get("Model");
    public static string Serial => Get("Serial");
    public static string Capacity => Get("Capacity");
    public static string Type => Get("Type");
    public static string Total => Get("Total");

    // Power
    public static string PowerTitle => Get("PowerTitle");
    public static string PowerSubtitle => Get("PowerSubtitle");
    public static string PowerActions => Get("PowerActions");
    public static string Shutdown => Get("Shutdown");
    public static string ShutdownDesc => Get("ShutdownDesc");
    public static string ForceShutdown => Get("ForceShutdown");
    public static string ForceRestart => Get("ForceRestart");
    public static string RestartPC => Get("RestartPC");
    public static string RestartDesc => Get("RestartDesc");
    public static string Sleep => Get("Sleep");
    public static string SleepDesc => Get("SleepDesc");
    public static string Hibernate => Get("Hibernate");
    public static string HibernateDesc => Get("HibernateDesc");
    public static string Lock => Get("Lock");
    public static string LockDesc => Get("LockDesc");
    public static string SignOut => Get("SignOut");
    public static string SignOutDesc => Get("SignOutDesc");
    public static string LogOff => Get("LogOff");
    public static string ScheduledActions => Get("ScheduledActions");
    public static string ScheduleShutdown => Get("ScheduleShutdown");
    public static string ScheduleRestart => Get("ScheduleRestart");
    public static string CancelScheduled => Get("CancelScheduled");
    public static string SystemTools => Get("SystemTools");
    public static string SystemConfiguration => Get("SystemConfiguration");
    public static string DirectXDiagnostics => Get("DirectXDiagnostics");
    public static string DeviceManager => Get("DeviceManager");
    public static string DiskManagement => Get("DiskManagement");
    public static string EventViewer => Get("EventViewer");
    public static string RegistryEditor => Get("RegistryEditor");
    public static string ControlPanel => Get("ControlPanel");
    public static string WindowsSettings => Get("WindowsSettings");

    // Common
    public static string Name => Get("Name");
    public static string Command => Get("Command");
    public static string Source => Get("Source");
    public static string Startup => Get("Startup");
    public static string ServiceName => Get("ServiceName");
    public static string Remove => Get("Remove");
    public static string OK => Get("OK");
    public static string Cancel => Get("Cancel");
    public static string Yes => Get("Yes");
    public static string No => Get("No");
    public static string Close => Get("Close");
    public static string Apply => Get("Apply");
    public static string Save => Get("Save");
    public static string Error => Get("Error");
    public static string Warning => Get("Warning");
    public static string Info => Get("Info");
    public static string Success => Get("Success");
    public static string Loading => Get("Loading");
    public static string LoadedIn => Get("LoadedIn");
    public static string ConfirmAction => Get("ConfirmAction");
    public static string AreYouSure => Get("AreYouSure");
    public static string AdminRequired => Get("AdminRequired");
    public static string AdminRequiredTitle => Get("AdminRequiredTitle");
    public static string RestartAsAdmin => Get("RestartAsAdmin");
    public static string RunningAsAdmin => Get("RunningAsAdmin");
    public static string PinOnTop => Get("PinOnTop");
    public static string UnpinFromTop => Get("UnpinFromTop");
    public static string Toggle => Get("Toggle");
    public static string Cancelled => Get("Cancelled");
    public static string ActionCannotBeUndone => Get("ActionCannotBeUndone");

    // Dialog Messages - Network
    public static string DnsFlushed => Get("DnsFlushed");
    public static string DnsFlushedMessage => Get("DnsFlushedMessage");
    public static string CouldNotFlushDns => Get("CouldNotFlushDns");

    // Dialog Messages - Power
    public static string ShutdownConfirm => Get("ShutdownConfirm");
    public static string CouldNotShutdown => Get("CouldNotShutdown");
    public static string ForceShutdownConfirm => Get("ForceShutdownConfirm");
    public static string CouldNotForceShutdown => Get("CouldNotForceShutdown");
    public static string RestartConfirm => Get("RestartConfirm");
    public static string CouldNotRestart => Get("CouldNotRestart");
    public static string ForceRestartConfirm => Get("ForceRestartConfirm");
    public static string CouldNotForceRestart => Get("CouldNotForceRestart");
    public static string CouldNotSleep => Get("CouldNotSleep");
    public static string CouldNotHibernate => Get("CouldNotHibernate");
    public static string LogOffConfirm => Get("LogOffConfirm");
    public static string CouldNotLogOff => Get("CouldNotLogOff");
    public static string CouldNotOpenTool => Get("CouldNotOpenTool");

    // Dialog Messages - Cleanup
    public static string NoSelection => Get("NoSelection");
    public static string PleaseSelectItem => Get("PleaseSelectItem");
    public static string ConfirmCleanup => Get("ConfirmCleanup");
    public static string CleanupConfirmMessage => Get("CleanupConfirmMessage");
    public static string CleanupCompleteMessage => Get("CleanupCompleteMessage");
    public static string CleanupCancelled => Get("CleanupCancelled");

    // Dialog Messages - Dashboard
    public static string CouldNotOpenGodMode => Get("CouldNotOpenGodMode");
    public static string ToggleDarkMode => Get("ToggleDarkMode");
    public static string ToggleDarkModeMessage => Get("ToggleDarkModeMessage");
    public static string CouldNotToggleDarkMode => Get("CouldNotToggleDarkMode");
    public static string RestartExplorerConfirm => Get("RestartExplorerConfirm");
    public static string RestartAudioDriver => Get("RestartAudioDriver");
    public static string RestartAudioDriverMessage => Get("RestartAudioDriverMessage");
    public static string CouldNotRestartAudio => Get("CouldNotRestartAudio");

    // Dialog Messages - Services
    public static string CouldNotStartService => Get("CouldNotStartService");
    public static string StopService => Get("StopService");
    public static string StopServiceConfirm => Get("StopServiceConfirm");
    public static string CouldNotStopService => Get("CouldNotStopService");
    public static string CouldNotRestartService => Get("CouldNotRestartService");

    // Dialog Messages - Task Manager
    public static string CouldNotOpenFileLocation => Get("CouldNotOpenFileLocation");
    public static string ProcessDetails => Get("ProcessDetails");
    public static string ProcessNotFound => Get("ProcessNotFound");
    public static string ProcessNotFoundMessage => Get("ProcessNotFoundMessage");

    // Dialog Messages - Autostart
    public static string CouldNotToggleAutostart => Get("CouldNotToggleAutostart");
    public static string CouldNotToggleAutostartTask => Get("CouldNotToggleAutostartTask");
    public static string CannotRemove => Get("CannotRemove");
    public static string CannotRemoveTaskScheduler => Get("CannotRemoveTaskScheduler");
    public static string RemoveAutostartEntry => Get("RemoveAutostartEntry");
    public static string RemoveAutostartConfirm => Get("RemoveAutostartConfirm");
    public static string RemoveRegistryNote => Get("RemoveRegistryNote");
    public static string RemoveStartupFolderNote => Get("RemoveStartupFolderNote");
    public static string CouldNotRemoveAutostart => Get("CouldNotRemoveAutostart");

    // Dialog Messages - Programs
    public static string UninstallProgram => Get("UninstallProgram");
    public static string UninstallConfirm => Get("UninstallConfirm");
    public static string CouldNotStartUninstaller => Get("CouldNotStartUninstaller");

    // Run Dialog
    public static string Run => Get("Run");
    public static string RunDialogTitle => Get("RunDialogTitle");
    public static string RunAsAdministrator => Get("RunAsAdministrator");
    public static string TypeCommand => Get("TypeCommand");
    public static string Open => Get("Open");
    public static string Browse => Get("Browse");
}
