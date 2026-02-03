using System.Diagnostics;
using System.Security.Principal;
using SysPilot.Controls;

namespace SysPilot.Helpers;

public static class AdminHelper
{
    private static bool? _isAdmin;

    /// <summary>
    /// Checks if the application is running with administrator privileges.
    /// </summary>
    public static bool IsAdmin
    {
        get
        {
            _isAdmin ??= CheckIsAdmin();
            return _isAdmin.Value;
        }
    }

    private static bool CheckIsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Restarts the application with administrator privileges.
    /// </summary>
    public static void RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            Environment.Exit(0);
        }
        catch
        {
            // User cancelled UAC or other error
        }
    }

    /// <summary>
    /// Checks if admin rights are available. If not, offers to restart as admin.
    /// </summary>
    /// <returns>True if admin, false if not</returns>
    public static bool RequireAdmin()
    {
        if (IsAdmin)
        {
            return true;
        }

        var result = CustomDialog.Show(
            Lang.AdminRequiredTitle,
            Lang.AdminRequired,
            CustomDialog.DialogType.Warning,
            Lang.RestartAsAdmin,
            Lang.Cancel);

        if (result == CustomDialog.ButtonResult.Primary)
        {
            RestartAsAdmin();
        }

        return false;
    }

    /// <summary>
    /// Tries to execute an action that requires admin rights.
    /// Shows error message if not admin.
    /// </summary>
    public static bool TryAdminAction(Action action)
    {
        if (!RequireAdmin()) return false;

        try
        {
            action();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            RequireAdmin();
            return false;
        }
    }

    /// <summary>
    /// Tries to execute an async action that requires admin rights.
    /// Shows error message if not admin.
    /// </summary>
    public static async Task<bool> TryAdminActionAsync(Func<Task> action)
    {
        if (!RequireAdmin()) return false;

        try
        {
            await action();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            RequireAdmin();
            return false;
        }
    }
}
