using System.Diagnostics;
using Microsoft.Win32;

namespace SysPilot.Helpers;

/// <summary>
/// Helper for reading installed programs from registry (fast alternative to WMI)
/// </summary>
public static class ProgramsHelper
{
    public class InstalledProgram
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Version { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public long EstimatedSize { get; set; } // In KB
        public string InstallLocation { get; set; } = "";
        public string UninstallString { get; set; } = "";
        public bool IsSystemComponent { get; set; }
        public string Source { get; set; } = ""; // Machine, Machine32, User

        public string SizeDisplay => EstimatedSize > 0 ? SystemHelper.FormatBytes(EstimatedSize * 1024) : "";

        public string InstallDateFormatted
        {
            get
            {
                if (string.IsNullOrEmpty(InstallDate) || InstallDate.Length != 8)
                    return "";
                try
                {
                    var year = InstallDate[..4];
                    var month = InstallDate[4..6];
                    var day = InstallDate[6..8];
                    return $"{day}.{month}.{year}";
                }
                catch
                {
                    return "";
                }
            }
        }
    }

    private static readonly string[] RegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public static List<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents = false)
    {
        var programs = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

        // Machine-wide installations (64-bit)
        ReadFromRegistry(RegistryHive.LocalMachine, RegistryPaths[0], "Machine", programs);

        // Machine-wide installations (32-bit on 64-bit Windows)
        ReadFromRegistry(RegistryHive.LocalMachine, RegistryPaths[1], "Machine32", programs);

        // Current user installations
        ReadFromRegistry(RegistryHive.CurrentUser, RegistryPaths[0], "User", programs);

        var result = programs.Values
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Where(p => includeSystemComponents || !p.IsSystemComponent)
            .Where(p => !string.IsNullOrEmpty(p.UninstallString)) // Must be uninstallable
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return [.. result];
    }

    private static void ReadFromRegistry(
        RegistryHive hive,
        string path,
        string source,
        Dictionary<string, InstalledProgram> programs)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var uninstallKey = baseKey.OpenSubKey(path);

            if (uninstallKey is null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    if (subKey is null)
                    {
                        continue;
                    }

                    var name = subKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    // Skip duplicates (prefer machine over user, 64-bit over 32-bit)
                    if (programs.ContainsKey(name)) continue;

                    var program = new InstalledProgram
                    {
                        Name = name,
                        Publisher = subKey.GetValue("Publisher") as string ?? "",
                        Version = subKey.GetValue("DisplayVersion") as string ?? "",
                        InstallDate = subKey.GetValue("InstallDate") as string ?? "",
                        InstallLocation = subKey.GetValue("InstallLocation") as string ?? "",
                        UninstallString = subKey.GetValue("UninstallString") as string ?? "",
                        IsSystemComponent = (subKey.GetValue("SystemComponent") as int?) == 1,
                        Source = source
                    };

                    // Get estimated size
                    var sizeValue = subKey.GetValue("EstimatedSize");
                    if (sizeValue is not null)
                    {
                        program.EstimatedSize = Convert.ToInt64(sizeValue);
                    }

                    programs[name] = program;
                }
                catch
                {
                    // Skip entries that can't be read
                }
            }
        }
        catch
        {
            // Registry path doesn't exist or access denied
        }
    }

    public static async Task<bool> UninstallProgramAsync(InstalledProgram program)
    {
        if (string.IsNullOrEmpty(program.UninstallString))
            return false;

        return await Task.Run(() =>
        {
            try
            {
                var uninstallString = program.UninstallString;

                // Let cmd.exe handle the parsing - most reliable for complex uninstall strings
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {uninstallString}",
                    UseShellExecute = true
                };

                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public static long GetTotalInstalledSize(List<InstalledProgram> programs)
    {
        return programs.Sum(p => p.EstimatedSize) * 1024; // Convert KB to bytes
    }
}
