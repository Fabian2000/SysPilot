using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SysPilot.Helpers;

/// <summary>
/// Helper class for network information and operations
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// Represents a network adapter with its configuration
    /// </summary>
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Speed { get; set; } = "";
        public List<string> IpAddresses { get; set; } = [];
        public List<string> DnsServers { get; set; } = [];
        public string Gateway { get; set; } = "";
    }

    /// <summary>
    /// Represents an active TCP connection
    /// </summary>
    public class TcpConnectionInfo
    {
        public string LocalAddress { get; set; } = "";
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; } = "";
        public int RemotePort { get; set; }
        public string State { get; set; } = "";
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
    }

    /// <summary>
    /// Gets all network adapters with their configuration
    /// </summary>
    public static List<NetworkAdapterInfo> GetNetworkAdapters()
    {
        var result = new List<NetworkAdapterInfo>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var info = new NetworkAdapterInfo
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    Type = nic.NetworkInterfaceType.ToString(),
                    Status = nic.OperationalStatus.ToString(),
                    MacAddress = FormatMacAddress(nic.GetPhysicalAddress()),
                    Speed = FormatSpeed(nic.Speed)
                };

                try
                {
                    var ipProps = nic.GetIPProperties();

                    // IP addresses
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork ||
                            addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            info.IpAddresses.Add(addr.Address.ToString());
                        }
                    }

                    // DNS servers
                    foreach (var dns in ipProps.DnsAddresses)
                    {
                        info.DnsServers.Add(dns.ToString());
                    }

                    // Default gateway
                    var gateway = ipProps.GatewayAddresses.FirstOrDefault();
                    if (gateway is not null)
                    {
                        info.Gateway = gateway.Address.ToString();
                    }
                }
                catch { }

                result.Add(info);
            }
        }
        catch { }

        return [.. result.OrderByDescending(a => a.Status == "Up").ThenBy(a => a.Name)];
    }

    /// <summary>
    /// Gets active TCP connections with process info (like netstat -b)
    /// </summary>
    public static List<TcpConnectionInfo> GetTcpConnections()
    {
        var result = new List<TcpConnectionInfo>();
        var processCache = new Dictionary<int, string>();

        try
        {
            // Try to get connections with process info using P/Invoke
            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);

            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedTcpTable(buffer, ref bufferSize, false, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                {
                    int numEntries = Marshal.ReadInt32(buffer);
                    var rowPtr = buffer + 4;

                    for (int i = 0; i < numEntries; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);

                        var info = new TcpConnectionInfo
                        {
                            State = GetTcpStateName(row.dwState),
                            LocalAddress = new IPAddress(row.dwLocalAddr).ToString(),
                            LocalPort = (int)((row.dwLocalPort >> 8) | ((row.dwLocalPort & 0xFF) << 8)),
                            RemoteAddress = new IPAddress(row.dwRemoteAddr).ToString(),
                            RemotePort = (int)((row.dwRemotePort >> 8) | ((row.dwRemotePort & 0xFF) << 8)),
                            ProcessId = (int)row.dwOwningPid
                        };

                        // Get process name (with caching)
                        if (!processCache.TryGetValue(info.ProcessId, out var processName))
                        {
                            try
                            {
                                using var proc = Process.GetProcessById(info.ProcessId);
                                processName = proc.ProcessName;
                            }
                            catch
                            {
                                processName = info.ProcessId == 0 ? "System Idle" : "Unknown";
                            }
                            processCache[info.ProcessId] = processName;
                        }
                        info.ProcessName = processName;

                        result.Add(info);
                        rowPtr += Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Fallback to basic method without process info
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var connections = properties.GetActiveTcpConnections();

                foreach (var conn in connections)
                {
                    result.Add(new TcpConnectionInfo
                    {
                        LocalAddress = conn.LocalEndPoint.Address.ToString(),
                        LocalPort = conn.LocalEndPoint.Port,
                        RemoteAddress = conn.RemoteEndPoint.Address.ToString(),
                        RemotePort = conn.RemoteEndPoint.Port,
                        State = conn.State.ToString(),
                        ProcessName = "-"
                    });
                }
            }
            catch { }
        }

        return [.. result.OrderBy(c => c.LocalPort)];
    }

    private static string GetTcpStateName(uint state)
    {
        return state switch
        {
            1 => "Closed",
            2 => "Listen",
            3 => "SynSent",
            4 => "SynReceived",
            5 => "Established",
            6 => "FinWait1",
            7 => "FinWait2",
            8 => "CloseWait",
            9 => "Closing",
            10 => "LastAck",
            11 => "TimeWait",
            12 => "DeleteTcb",
            _ => "Unknown"
        };
    }

    // P/Invoke declarations for GetExtendedTcpTable
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        int tblClass,
        int reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public uint dwOwningPid;
    }

    /// <summary>
    /// Gets TCP listeners (listening ports)
    /// </summary>
    public static List<(string Address, int Port)> GetTcpListeners()
    {
        var result = new List<(string, int)>();

        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            foreach (var listener in properties.GetActiveTcpListeners())
            {
                result.Add((listener.Address.ToString(), listener.Port));
            }
        }
        catch { }

        return [.. result.OrderBy(l => l.Item2)];
    }

    /// <summary>
    /// Pings a host and returns the result
    /// </summary>
    public static async Task<(bool Success, long RoundtripMs, string Status)> PingAsync(string host, int timeoutMs = 3000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return (reply.Status == IPStatus.Success, reply.RoundtripTime, reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    /// <summary>
    /// Gets public IP address
    /// </summary>
    public static async Task<string> GetPublicIpAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var ip = await client.GetStringAsync("https://api.ipify.org");
            return ip.Trim();
        }
        catch
        {
            return "Unable to determine";
        }
    }

    /// <summary>
    /// Gets the local IP address of the primary network interface
    /// </summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    private static string FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 0)
        {
            return "N/A";
        }
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static string FormatSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond < 0)
        {
            return "Unknown";
        }
        if (bitsPerSecond >= 1_000_000_000)
        {
            return $"{bitsPerSecond / 1_000_000_000.0:0.#} Gbps";
        }
        if (bitsPerSecond >= 1_000_000)
        {
            return $"{bitsPerSecond / 1_000_000.0:0.#} Mbps";
        }
        if (bitsPerSecond >= 1_000)
        {
            return $"{bitsPerSecond / 1_000.0:0.#} Kbps";
        }
        return $"{bitsPerSecond} bps";
    }
}
