using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace SysPilot.Installer.Install;

internal record ShortcutOptions(bool StartMenuAllUsers, bool StartMenuCurrentUser, bool Desktop);

internal static class PayloadInstaller
{
    public static void Install(string targetDir, ShortcutOptions shortcuts, Action<float, string> progress)
    {
        progress(0.05f, "Creating directory...");

        // Ensure target directory exists
        Directory.CreateDirectory(targetDir);

        progress(0.1f, "Extracting files...");

        // Extract payload from embedded resource
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SysPilot.Installer.Resources.Payload.zip")
            ?? throw new Exception("Payload not found in installer.");

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        int total = entries.Count;
        int current = 0;

        foreach (var entry in entries)
        {
            var destPath = Path.Combine(targetDir, entry.FullName);
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            using var entryStream = entry.Open();
            using var fileStream = File.Create(destPath);
            entryStream.CopyTo(fileStream);

            current++;
            float fileProgress = 0.1f + (0.7f * current / total);
            progress(fileProgress, $"Extracting {entry.Name}...");
        }

        progress(0.85f, "Creating shortcuts...");

        // Uninstall.exe is already in the payload
        var uninstallerPath = Path.Combine(targetDir, "Uninstall.exe");

        // Create shortcuts based on user selection
        var exePath = Path.Combine(targetDir, "SysPilot.exe");

        if (shortcuts.Desktop)
        {
            Shortcuts.CreateDesktopShortcut("SysPilot", exePath, targetDir);
        }

        if (shortcuts.StartMenuAllUsers)
        {
            Shortcuts.CreateStartMenuShortcut("SysPilot", exePath, targetDir, allUsers: true);
            Shortcuts.CreateUninstallShortcut(targetDir, uninstallerPath, allUsers: true);
        }

        if (shortcuts.StartMenuCurrentUser)
        {
            Shortcuts.CreateStartMenuShortcut("SysPilot", exePath, targetDir, allUsers: false);
            Shortcuts.CreateUninstallShortcut(targetDir, uninstallerPath, allUsers: false);
        }

        progress(0.95f, "Registering application...");

        // Register in Add/Remove Programs
        RegistryHelper.RegisterUninstaller(targetDir, exePath, uninstallerPath);

        progress(1.0f, "Installation complete!");
    }
}
