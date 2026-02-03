using System.Runtime.InteropServices;

namespace SysPilot.Helpers;

/// <summary>
/// P/Invoke declarations for Windows API functions
/// </summary>
internal static partial class NativeMethods
{
    // ExitWindowsEx flags
    public const uint EWX_LOGOFF = 0x00000000;
    public const uint EWX_SHUTDOWN = 0x00000001;
    public const uint EWX_REBOOT = 0x00000002;
    public const uint EWX_FORCE = 0x00000004;
    public const uint EWX_POWEROFF = 0x00000008;
    public const uint EWX_FORCEIFHUNG = 0x00000010;

    // Shutdown reason
    public const uint SHTDN_REASON_MAJOR_APPLICATION = 0x00040000;
    public const uint SHTDN_REASON_MINOR_OTHER = 0x00000000;
    public const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;

    // Token privileges
    public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
    public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    public const uint TOKEN_QUERY = 0x0008;
    public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ExitWindowsEx(uint uFlags, uint dwReason);

    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(
        nint ProcessHandle,
        uint DesiredAccess,
        out nint TokenHandle);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool LookupPrivilegeValue(
        string? lpSystemName,
        string lpName,
        out LUID lpLuid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AdjustTokenPrivileges(
        nint TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        nint PreviousState,
        nint ReturnLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    [LibraryImport("kernel32.dll")]
    public static partial nint GetCurrentProcess();

    // Desktop icon visibility
    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindow(string lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string lpszClass, string? lpszWindow);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hWnd);

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }

    /// <summary>
    /// Enables shutdown privilege for the current process
    /// </summary>
    public static bool EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out nint tokenHandle))
        {
            return false;
        }

        try
        {
            if (!LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, out LUID luid))
            {
                return false;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                }
            };

            return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, nint.Zero, nint.Zero);
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }
}
