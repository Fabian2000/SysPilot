using System.ServiceProcess;
using Microsoft.Win32;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for Windows service management
/// </summary>
public static class ServiceHelper
{
    /// <summary>
    /// Represents service information for display
    /// </summary>
    public class ServiceInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "";
        public string StartType { get; set; } = "";
        public bool CanStop { get; set; }
        public bool CanStart { get; set; }
    }

    /// <summary>
    /// Gets all Windows services
    /// </summary>
    public static List<ServiceInfo> GetServices()
    {
        var result = new List<ServiceInfo>();

        try
        {
            foreach (var service in ServiceController.GetServices())
            {
                try
                {
                    result.Add(new ServiceInfo
                    {
                        Name = service.ServiceName,
                        DisplayName = service.DisplayName,
                        Status = service.Status.ToString(),
                        StartType = GetStartType(service.ServiceName),
                        CanStop = service.CanStop && service.Status == ServiceControllerStatus.Running,
                        CanStart = service.Status == ServiceControllerStatus.Stopped
                    });
                }
                catch { }
                finally
                {
                    service.Dispose();
                }
            }
        }
        catch { }

        return [.. result.OrderBy(s => s.DisplayName)];
    }

    private static string GetStartType(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("Start") is int start)
            {
                return start switch
                {
                    0 => "Boot",
                    1 => "System",
                    2 => "Automatic",
                    3 => "Manual",
                    4 => "Disabled",
                    _ => "Unknown"
                };
            }
        }
        catch { }
        return "Unknown";
    }

    /// <summary>
    /// Starts a service
    /// </summary>
    public static async Task<bool> StartServiceAsync(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                return true;
            }

            service.Start();
            await Task.Run(() => service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops a service
    /// </summary>
    public static async Task<bool> StopServiceAsync(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                return true;
            }

            if (!service.CanStop)
            {
                return false;
            }

            service.Stop();
            await Task.Run(() => service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
            return service.Status == ServiceControllerStatus.Stopped;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts a service
    /// </summary>
    public static async Task<bool> RestartServiceAsync(string serviceName)
    {
        if (!await StopServiceAsync(serviceName))
        {
            return false;
        }
        await Task.Delay(500);
        return await StartServiceAsync(serviceName);
    }

    /// <summary>
    /// Sets the startup type of a service
    /// </summary>
    public static bool SetStartupType(string serviceName, string startType)
    {
        try
        {
            int value = startType switch
            {
                "Automatic" => 2,
                "Manual" => 3,
                "Disabled" => 4,
                _ => -1
            };

            if (value == -1)
            {
                return false;
            }

            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: true);
            if (key is null)
            {
                return false;
            }

            key.SetValue("Start", value, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts the Windows Audio service
    /// </summary>
    public static async Task<bool> RestartAudioServiceAsync()
    {
        return await RestartServiceAsync("Audiosrv");
    }
}
