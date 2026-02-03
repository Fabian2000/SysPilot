using System.Runtime.InteropServices;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for power management operations
/// </summary>
public static partial class PowerHelper
{
    /// <summary>
    /// Shuts down the computer
    /// </summary>
    public static bool Shutdown()
    {
        NativeMethods.EnableShutdownPrivilege();
        return NativeMethods.ExitWindowsEx(
            NativeMethods.EWX_SHUTDOWN | NativeMethods.EWX_FORCEIFHUNG,
            NativeMethods.SHTDN_REASON_MAJOR_APPLICATION | NativeMethods.SHTDN_REASON_FLAG_PLANNED);
    }

    /// <summary>
    /// Restarts the computer
    /// </summary>
    public static bool Restart()
    {
        NativeMethods.EnableShutdownPrivilege();
        return NativeMethods.ExitWindowsEx(
            NativeMethods.EWX_REBOOT | NativeMethods.EWX_FORCEIFHUNG,
            NativeMethods.SHTDN_REASON_MAJOR_APPLICATION | NativeMethods.SHTDN_REASON_FLAG_PLANNED);
    }

    /// <summary>
    /// Force shuts down the computer (closes all applications without saving)
    /// </summary>
    public static bool ForceShutdown()
    {
        NativeMethods.EnableShutdownPrivilege();
        return NativeMethods.ExitWindowsEx(
            NativeMethods.EWX_SHUTDOWN | NativeMethods.EWX_FORCE,
            NativeMethods.SHTDN_REASON_MAJOR_APPLICATION | NativeMethods.SHTDN_REASON_FLAG_PLANNED);
    }

    /// <summary>
    /// Force restarts the computer (closes all applications without saving)
    /// </summary>
    public static bool ForceRestart()
    {
        NativeMethods.EnableShutdownPrivilege();
        return NativeMethods.ExitWindowsEx(
            NativeMethods.EWX_REBOOT | NativeMethods.EWX_FORCE,
            NativeMethods.SHTDN_REASON_MAJOR_APPLICATION | NativeMethods.SHTDN_REASON_FLAG_PLANNED);
    }

    /// <summary>
    /// Logs off the current user
    /// </summary>
    public static bool Logoff()
    {
        return NativeMethods.ExitWindowsEx(
            NativeMethods.EWX_LOGOFF | NativeMethods.EWX_FORCEIFHUNG,
            NativeMethods.SHTDN_REASON_MAJOR_APPLICATION);
    }

    /// <summary>
    /// Puts the computer to sleep
    /// </summary>
    public static bool Sleep()
    {
        return NativeMethods.SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
    }

    /// <summary>
    /// Hibernates the computer
    /// </summary>
    public static bool Hibernate()
    {
        return NativeMethods.SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false);
    }

    /// <summary>
    /// Locks the workstation
    /// </summary>
    public static bool Lock()
    {
        return LockWorkStation();
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LockWorkStation();
}
