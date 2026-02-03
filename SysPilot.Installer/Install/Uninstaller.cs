using System.IO;
using System.Runtime.InteropServices;
using SysPilot.Installer.Native;

namespace SysPilot.Installer.Install;

internal static class Uninstaller
{
    public static void RemoveShortcuts()
    {
        // Desktop shortcut
        try
        {
            var desktopPath = GetKnownFolderPath(Shell32.FOLDERID_Desktop);
            if (desktopPath is not null)
            {
                var linkPath = Path.Combine(desktopPath, "SysPilot.lnk");
                if (File.Exists(linkPath))
                {
                    File.Delete(linkPath);
                }
            }
        }
        catch { }

        // Start Menu shortcut folder (All Users)
        try
        {
            var commonProgramsPath = GetKnownFolderPath(Shell32.FOLDERID_CommonPrograms);
            if (commonProgramsPath is not null)
            {
                var folderPath = Path.Combine(commonProgramsPath, "SysPilot");
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
            }
        }
        catch { }

        // Start Menu shortcut folder (Current User)
        try
        {
            var userProgramsPath = GetKnownFolderPath(Shell32.FOLDERID_Programs);
            if (userProgramsPath is not null)
            {
                var folderPath = Path.Combine(userProgramsPath, "SysPilot");
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
            }
        }
        catch { }
    }

    private static string? GetKnownFolderPath(Guid folderId)
    {
        int hr = Shell32.SHGetKnownFolderPath(ref folderId, 0, 0, out var path);
        if (hr != 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(path);
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }
}
