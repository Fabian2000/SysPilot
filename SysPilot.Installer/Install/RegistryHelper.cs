using System.IO;
using Microsoft.Win32;

namespace SysPilot.Installer.Install;

internal static class RegistryHelper
{
    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SysPilot";

    public static bool IsInstalled(out string? installDir)
    {
        installDir = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath);
            if (key is null)
            {
                return false;
            }

            installDir = key.GetValue("InstallLocation") as string;
            return !string.IsNullOrEmpty(installDir) && Directory.Exists(installDir);
        }
        catch
        {
            return false;
        }
    }

    public static void RegisterUninstaller(string installDir, string exePath, string uninstallerPath)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath);
            if (key is null)
            {
                return;
            }

            // Calculate approximate size (in KB)
            long sizeKB = 0;
            try
            {
                var dirInfo = new DirectoryInfo(installDir);
                sizeKB = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024;
            }
            catch { }

            key.SetValue("DisplayName", "SysPilot");
            key.SetValue("DisplayIcon", exePath);
            key.SetValue("DisplayVersion", "3.0.0");
            key.SetValue("Publisher", "SysPilot");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("UninstallString", $"\"{uninstallerPath}\" -uninstall");
            key.SetValue("QuietUninstallString", $"\"{uninstallerPath}\" -uninstall -quiet");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)sizeKB, RegistryValueKind.DWord);
        }
        catch
        {
            // Registry access might fail without admin rights
            // Installation continues, just won't show in Add/Remove Programs
        }
    }

    public static void RemoveUninstaller()
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(UninstallKeyPath, false);
        }
        catch { }
    }
}
