using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SysPilot.Installer.Native;

namespace SysPilot.Installer.Install;

internal static class Shortcuts
{
    public static void CreateDesktopShortcut(string name, string targetPath, string workingDir)
    {
        var desktopPath = GetKnownFolderPath(Shell32.FOLDERID_Desktop);
        if (string.IsNullOrEmpty(desktopPath))
        {
            return;
        }

        var linkPath = Path.Combine(desktopPath, $"{name}.lnk");
        CreateShortcutViaPS(linkPath, targetPath, workingDir, name);
    }

    public static void CreateStartMenuShortcut(string name, string targetPath, string workingDir, bool allUsers)
    {
        // All Users: C:\ProgramData\Microsoft\Windows\Start Menu\Programs\
        // Current User: C:\Users\<user>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\
        var folderId = allUsers ? Shell32.FOLDERID_CommonPrograms : Shell32.FOLDERID_Programs;
        var programsPath = GetKnownFolderPath(folderId);
        if (string.IsNullOrEmpty(programsPath))
        {
            return;
        }

        // Create SysPilot folder in Start Menu
        var folderPath = Path.Combine(programsPath, name);
        Directory.CreateDirectory(folderPath);

        var linkPath = Path.Combine(folderPath, $"{name}.lnk");
        CreateShortcutViaPS(linkPath, targetPath, workingDir, name);
    }

    public static void CreateUninstallShortcut(string installDir, string uninstallerPath, bool allUsers)
    {
        var folderId = allUsers ? Shell32.FOLDERID_CommonPrograms : Shell32.FOLDERID_Programs;
        var programsPath = GetKnownFolderPath(folderId);
        if (string.IsNullOrEmpty(programsPath))
        {
            return;
        }

        var folderPath = Path.Combine(programsPath, "SysPilot");
        Directory.CreateDirectory(folderPath);

        var linkPath = Path.Combine(folderPath, "Uninstall SysPilot.lnk");
        CreateShortcutViaPS(linkPath, uninstallerPath, installDir, "Uninstall SysPilot");
    }

    private static void CreateShortcutViaPS(string linkPath, string targetPath, string workingDir, string description, string? arguments = null)
    {
        try
        {
            // Use PowerShell to create shortcut (works reliably with NativeAOT)
            var argsLine = string.IsNullOrEmpty(arguments) ? "" : $"$s.Arguments = '{arguments}';";
            var script = $@"
$WshShell = New-Object -ComObject WScript.Shell;
$s = $WshShell.CreateShortcut('{linkPath.Replace("'", "''")}');
$s.TargetPath = '{targetPath.Replace("'", "''")}';
$s.WorkingDirectory = '{workingDir.Replace("'", "''")}';
$s.Description = '{description.Replace("'", "''")}';
$s.IconLocation = '{targetPath.Replace("'", "''")}';
{argsLine}
$s.Save()";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch
        {
            // Shortcut creation is optional, don't fail installation
        }
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
