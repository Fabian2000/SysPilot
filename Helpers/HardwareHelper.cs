using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace SysPilot.Helpers;

/// <summary>
/// Helper for retrieving detailed hardware information via WMI
/// </summary>
public static class HardwareHelper
{
    public class HardwareInfo
    {
        public CpuInfo Cpu { get; set; } = new();
        public List<GpuInfo> Gpus { get; set; } = [];
        public MotherboardInfo Motherboard { get; set; } = new();
        public BiosInfo Bios { get; set; } = new();
        public List<RamModuleInfo> RamModules { get; set; } = [];
        public List<DiskInfo> Disks { get; set; } = [];
        public List<NetworkAdapterInfo> NetworkAdapters { get; set; } = [];
        public List<AudioDeviceInfo> AudioDevices { get; set; } = [];
        public OsInfo Os { get; set; } = new();
    }

    public class CpuInfo
    {
        public string Name { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public int Cores { get; set; }
        public int Threads { get; set; }
        public string BaseSpeed { get; set; } = "";
        public string MaxSpeed { get; set; } = "";
        public string Socket { get; set; } = "";
        public string L2Cache { get; set; } = "";
        public string L3Cache { get; set; } = "";
        public string Architecture { get; set; } = "";
    }

    public class GpuInfo
    {
        public string Name { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string DriverVersion { get; set; } = "";
        public string VideoMemory { get; set; } = "";
        public string Resolution { get; set; } = "";
        public string RefreshRate { get; set; } = "";
    }

    public class MotherboardInfo
    {
        public string Manufacturer { get; set; } = "";
        public string Product { get; set; } = "";
        public string Version { get; set; } = "";
        public string SerialNumber { get; set; } = "";
    }

    public class BiosInfo
    {
        public string Manufacturer { get; set; } = "";
        public string Version { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public string Mode { get; set; } = ""; // UEFI or Legacy
    }

    public class RamModuleInfo
    {
        public string Manufacturer { get; set; } = "";
        public string Capacity { get; set; } = "";
        public long CapacityBytes { get; set; }
        public string Speed { get; set; } = "";
        public string Type { get; set; } = "";
        public string FormFactor { get; set; } = "";
        public string Slot { get; set; } = "";
        public string PartNumber { get; set; } = "";
    }

    public class DiskInfo
    {
        public string Model { get; set; } = "";
        public string MediaType { get; set; } = ""; // SSD, HDD
        public string Capacity { get; set; } = "";
        public string Interface { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public int Index { get; set; }
    }

    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Speed { get; set; } = "";
        public string ConnectionType { get; set; } = "";
        public bool IsPhysical { get; set; }
    }

    public class AudioDeviceInfo
    {
        public string Name { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class OsInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Build { get; set; } = "";
        public string Architecture { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public string LastBoot { get; set; } = "";
        public string ComputerName { get; set; } = "";
        public string UserName { get; set; } = "";
    }

    public static HardwareInfo GetHardwareInfo()
    {
        var info = new HardwareInfo();

        try { info.Cpu = GetCpuInfo(); } catch { info.Cpu = new CpuInfo(); }
        try { info.Gpus = GetGpuInfo(); } catch { info.Gpus = []; }
        try { info.Motherboard = GetMotherboardInfo(); } catch { info.Motherboard = new MotherboardInfo(); }
        try { info.Bios = GetBiosInfo(); } catch { info.Bios = new BiosInfo(); }
        try { info.RamModules = GetRamModules(); } catch { info.RamModules = []; }
        try { info.Disks = GetDiskInfo(); } catch { info.Disks = []; }
        try { info.NetworkAdapters = GetNetworkAdapters(); } catch { info.NetworkAdapters = []; }
        try { info.AudioDevices = GetAudioDevices(); } catch { info.AudioDevices = []; }
        try { info.Os = GetOsInfo(); } catch { info.Os = new OsInfo(); }

        return info;
    }

    private static CpuInfo GetCpuInfo()
    {
        var cpu = new CpuInfo();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation, L2CacheSize, L3CacheSize, Architecture FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                cpu.Name = obj["Name"]?.ToString()?.Trim() ?? "";
                cpu.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                cpu.Cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                cpu.Threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                var maxSpeed = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                cpu.MaxSpeed = maxSpeed > 0 ? $"{maxSpeed / 1000.0:0.00} GHz" : "";
                cpu.BaseSpeed = cpu.MaxSpeed; // Base speed not directly available
                cpu.Socket = obj["SocketDesignation"]?.ToString() ?? "";
                var l2 = Convert.ToInt64(obj["L2CacheSize"] ?? 0) * 1024;
                var l3 = Convert.ToInt64(obj["L3CacheSize"] ?? 0) * 1024;
                cpu.L2Cache = l2 > 0 ? SystemHelper.FormatBytes(l2) : "";
                cpu.L3Cache = l3 > 0 ? SystemHelper.FormatBytes(l3) : "";
                var arch = Convert.ToInt32(obj["Architecture"] ?? 0);
                cpu.Architecture = arch switch
                {
                    0 => "x86",
                    9 => "x64",
                    12 => "ARM64",
                    _ => ""
                };
                break;
            }
        }
        catch { }
        return cpu;
    }

    private static List<GpuInfo> GetGpuInfo()
    {
        var gpus = new List<GpuInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterCompatibility, DriverVersion, AdapterRAM, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var gpu = new GpuInfo
                {
                    Name = obj["Name"]?.ToString() ?? "",
                    Manufacturer = obj["AdapterCompatibility"]?.ToString() ?? "",
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? ""
                };

                var vram = Convert.ToInt64(obj["AdapterRAM"] ?? 0);
                gpu.VideoMemory = vram > 0 ? SystemHelper.FormatBytes(vram) : "";

                var hRes = Convert.ToInt32(obj["CurrentHorizontalResolution"] ?? 0);
                var vRes = Convert.ToInt32(obj["CurrentVerticalResolution"] ?? 0);
                if (hRes > 0 && vRes > 0)
                {
                    gpu.Resolution = $"{hRes} x {vRes}";
                }

                var refresh = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 0);
                gpu.RefreshRate = refresh > 0 ? $"{refresh} Hz" : "";

                gpus.Add(gpu);
            }
        }
        catch { }
        return gpus;
    }

    private static MotherboardInfo GetMotherboardInfo()
    {
        var mb = new MotherboardInfo();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Product, Version, SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                mb.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                mb.Product = obj["Product"]?.ToString() ?? "";
                mb.Version = obj["Version"]?.ToString() ?? "";
                mb.SerialNumber = obj["SerialNumber"]?.ToString() ?? "";
                break;
            }
        }
        catch { }
        return mb;
    }

    private static BiosInfo GetBiosInfo()
    {
        var bios = new BiosInfo();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                bios.Manufacturer = obj["Manufacturer"]?.ToString() ?? "";
                bios.Version = obj["SMBIOSBIOSVersion"]?.ToString() ?? "";
                var dateStr = obj["ReleaseDate"]?.ToString();
                if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 8)
                {
                    bios.ReleaseDate = $"{dateStr[6..8]}.{dateStr[4..6]}.{dateStr[..4]}";
                }
                break;
            }

            // Check UEFI mode
            var firmwareType = Environment.GetEnvironmentVariable("firmware_type");
            bios.Mode = Environment.GetFolderPath(Environment.SpecialFolder.System).Contains("EFI") ? "UEFI" : "Legacy";

            // Better detection via registry
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                if (key is not null)
                {
                    bios.Mode = "UEFI (Secure Boot)";
                }
                else
                {
                    using var key2 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\EFI");
                    bios.Mode = key2 is not null ? "UEFI" : "Legacy";
                }
            }
            catch { }
        }
        catch { }
        return bios;
    }

    private static List<RamModuleInfo> GetRamModules()
    {
        var modules = new List<RamModuleInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Capacity, Speed, MemoryType, FormFactor, DeviceLocator, PartNumber FROM Win32_PhysicalMemory");
            foreach (var obj in searcher.Get())
            {
                var module = new RamModuleInfo
                {
                    Manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "",
                    PartNumber = obj["PartNumber"]?.ToString()?.Trim() ?? "",
                    Slot = obj["DeviceLocator"]?.ToString() ?? ""
                };

                var capacity = Convert.ToInt64(obj["Capacity"] ?? 0);
                module.CapacityBytes = capacity;
                module.Capacity = capacity > 0 ? SystemHelper.FormatBytes(capacity) : "";

                var speed = Convert.ToInt32(obj["Speed"] ?? 0);
                module.Speed = speed > 0 ? $"{speed} MHz" : "";

                var memType = Convert.ToInt32(obj["MemoryType"] ?? 0);
                module.Type = memType switch
                {
                    20 => "DDR",
                    21 => "DDR2",
                    22 => "DDR2 FB-DIMM",
                    24 => "DDR3",
                    26 => "DDR4",
                    34 => "DDR5",
                    _ => memType == 0 ? "DDR4/DDR5" : $"Type {memType}"
                };

                var formFactor = Convert.ToInt32(obj["FormFactor"] ?? 0);
                module.FormFactor = formFactor switch
                {
                    8 => "DIMM",
                    12 => "SO-DIMM",
                    _ => ""
                };

                modules.Add(module);
            }
        }
        catch { }
        return modules;
    }

    private static List<DiskInfo> GetDiskInfo()
    {
        var disks = new List<DiskInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, Size, InterfaceType, SerialNumber, Index, Status, MediaType FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var disk = new DiskInfo
                {
                    Model = obj["Model"]?.ToString() ?? "",
                    Interface = obj["InterfaceType"]?.ToString() ?? "",
                    SerialNumber = obj["SerialNumber"]?.ToString()?.Trim() ?? "",
                    Index = Convert.ToInt32(obj["Index"] ?? 0),
                    Status = obj["Status"]?.ToString() ?? ""
                };

                var size = Convert.ToInt64(obj["Size"] ?? 0);
                disk.Capacity = size > 0 ? SystemHelper.FormatBytes(size) : "";

                // MediaType detection
                var mediaType = obj["MediaType"]?.ToString() ?? "";
                if (mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                    disk.Model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                    disk.Model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                {
                    disk.MediaType = "SSD";
                }
                else if (mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase))
                {
                    // Could be SSD or HDD - check interface
                    disk.MediaType = disk.Interface.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ? "NVMe SSD" : "HDD/SSD";
                }
                else
                {
                    disk.MediaType = mediaType;
                }

                disks.Add(disk);
            }

            // Try to get more accurate SSD info from MSFT_PhysicalDisk
            try
            {
                using var searcher2 = new ManagementObjectSearcher(@"\\.\ROOT\Microsoft\Windows\Storage",
                    "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk");
                var physicalDisks = new Dictionary<int, string>();
                foreach (var obj in searcher2.Get())
                {
                    var deviceId = Convert.ToInt32(obj["DeviceId"]);
                    var type = Convert.ToInt32(obj["MediaType"] ?? 0);
                    physicalDisks[deviceId] = type switch
                    {
                        3 => "HDD",
                        4 => "SSD",
                        5 => "SCM",
                        _ => ""
                    };
                }

                foreach (var disk in disks)
                {
                    if (physicalDisks.TryGetValue(disk.Index, out var type) && !string.IsNullOrEmpty(type))
                    {
                        if (disk.Interface.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                        {
                            disk.MediaType = "NVMe SSD";
                        }
                        else
                        {
                            disk.MediaType = type;
                        }
                    }
                }
            }
            catch { }
        }
        catch { }
        return [.. disks.OrderBy(d => d.Index)];
    }

    private static List<NetworkAdapterInfo> GetNetworkAdapters()
    {
        var adapters = new List<NetworkAdapterInfo>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var adapter = new NetworkAdapterInfo
                {
                    Name = ni.Name,
                    MacAddress = ni.GetPhysicalAddress().ToString(),
                    ConnectionType = ni.NetworkInterfaceType.ToString(),
                    IsPhysical = ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                                 !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                };

                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    var speed = ni.Speed;
                    if (speed > 0 && speed < long.MaxValue)
                    {
                        adapter.Speed = speed >= 1_000_000_000
                            ? $"{speed / 1_000_000_000.0:0.#} Gbps"
                            : $"{speed / 1_000_000.0:0} Mbps";
                    }
                }

                // Format MAC address
                if (adapter.MacAddress.Length == 12)
                {
                    adapter.MacAddress = string.Join(":", Enumerable.Range(0, 6)
                        .Select(i => adapter.MacAddress.Substring(i * 2, 2)));
                }

                adapters.Add(adapter);
            }
        }
        catch { }
        return [.. adapters.OrderByDescending(a => a.IsPhysical).ThenBy(a => a.Name)];
    }

    private static List<AudioDeviceInfo> GetAudioDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, Status FROM Win32_SoundDevice");
            foreach (var obj in searcher.Get())
            {
                devices.Add(new AudioDeviceInfo
                {
                    Name = obj["Name"]?.ToString() ?? "",
                    Manufacturer = obj["Manufacturer"]?.ToString() ?? "",
                    Status = obj["Status"]?.ToString() ?? ""
                });
            }
        }
        catch { }
        return devices;
    }

    private static OsInfo GetOsInfo()
    {
        var os = new OsInfo
        {
            ComputerName = Environment.MachineName,
            UserName = Environment.UserName,
            Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"
        };

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, Version, BuildNumber, InstallDate, LastBootUpTime FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                os.Name = obj["Caption"]?.ToString() ?? "";
                os.Version = obj["Version"]?.ToString() ?? "";
                os.Build = obj["BuildNumber"]?.ToString() ?? "";

                var installDate = obj["InstallDate"]?.ToString();
                if (!string.IsNullOrEmpty(installDate) && installDate.Length >= 8)
                {
                    os.InstallDate = $"{installDate[6..8]}.{installDate[4..6]}.{installDate[..4]}";
                }

                var lastBoot = obj["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(lastBoot) && lastBoot.Length >= 14)
                {
                    os.LastBoot = $"{lastBoot[6..8]}.{lastBoot[4..6]}.{lastBoot[..4]} {lastBoot[8..10]}:{lastBoot[10..12]}";
                }
                break;
            }
        }
        catch { }
        return os;
    }
}
