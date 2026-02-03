using System.Runtime.InteropServices;
using SysPilot.Installer.UI;
using SysPilot.Installer.Install;

namespace SysPilot.Installer;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            CoInitializeEx(0, 2);

            bool isInstalled = RegistryHelper.IsInstalled(out string? installDir);

            // Show installer window (in uninstall mode if already installed)
            using var window = new InstallerWindow(isInstalled ? installDir : null);
            window.Show();
            window.Run();
        }
        catch (Exception ex)
        {
            MessageBoxW(0, $"Error: {ex.Message}\n\n{ex.StackTrace}", "SysPilot Setup", 0x10);
        }
    }
}
