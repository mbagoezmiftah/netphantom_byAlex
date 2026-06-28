using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using NetPhantom.Models;
using SharpPcap;

namespace NetPhantom.Services;

public static class NetworkHelper
{
    /// <summary>Returns all active physical/virtual adapters with SharpPcap device names.</summary>
    public static List<NetworkInterfaceInfo> GetInterfaces()
    {
        var result = new List<NetworkInterfaceInfo>();

        // Build a MAC→SharpPcap device map
        var pcapDevices = CaptureDeviceList.Instance;
        var pcapByMac = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dev in pcapDevices)
        {
            // SharpPcap device name is dev.Name; friendly name via Interface property
            var iface = dev.Interface;
            if (iface?.MacAddress != null)
            {
                var mac = string.Join(":", iface.MacAddress.GetAddressBytes().Select(b => b.ToString("X2")));
                pcapByMac[mac] = dev.Name;
            }
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = nic.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            var mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
            pcapByMac.TryGetValue(mac, out var deviceName);

            result.Add(new NetworkInterfaceInfo
            {
                Name = nic.Name,
                Description = nic.Description,
                IpAddress = ipv4?.Address.ToString() ?? string.Empty,
                MacAddress = mac,
                DeviceName = deviceName ?? string.Empty
            });
        }

        return result;
    }

    /// <summary>Returns the default gateway IP for the given local IP, or null.</summary>
    public static string? GetDefaultGateway(string localIp)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            var props = nic.GetIPProperties();
            var hasLocal = props.UnicastAddresses
                .Any(a => a.Address.ToString() == localIp);
            if (!hasLocal) continue;

            var gw = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return gw?.Address.ToString();
        }
        return null;
    }

    /// <summary>Expand a CIDR or range string into a list of IP addresses.</summary>
    public static IEnumerable<IPAddress> ExpandRange(string range)
    {
        range = range.Trim();

        // CIDR: 192.168.1.0/24
        if (range.Contains('/'))
        {
            var parts = range.Split('/');
            if (IPAddress.TryParse(parts[0], out var network) &&
                int.TryParse(parts[1], out var prefix))
            {
                var baseIp = BitConverter.ToUInt32(network.GetAddressBytes().Reverse().ToArray(), 0);
                var mask = prefix == 0 ? 0u : 0xFFFFFFFFu << (32 - prefix);
                var start = baseIp & mask;
                var end = start | ~mask;
                for (var i = start + 1; i < end; i++)
                {
                    var bytes = BitConverter.GetBytes(i).Reverse().ToArray();
                    yield return new IPAddress(bytes);
                }
            }
            yield break;
        }

        // Range: 192.168.1.1-192.168.1.254 or 192.168.1.1-254
        if (range.Contains('-'))
        {
            var parts = range.Split('-');
            if (IPAddress.TryParse(parts[0].Trim(), out var startIp))
            {
                var endStr = parts[1].Trim();
                IPAddress? endIp;
                if (!IPAddress.TryParse(endStr, out endIp))
                {
                    // Short form: last octet only
                    var prefix2 = parts[0].Trim().LastIndexOf('.');
                    if (prefix2 >= 0)
                        IPAddress.TryParse(parts[0].Trim()[..(prefix2 + 1)] + endStr, out endIp);
                }
                if (endIp != null)
                {
                    var s = BitConverter.ToUInt32(startIp.GetAddressBytes().Reverse().ToArray(), 0);
                    var e = BitConverter.ToUInt32(endIp.GetAddressBytes().Reverse().ToArray(), 0);
                    for (var i = s; i <= e; i++)
                    {
                        var bytes = BitConverter.GetBytes(i).Reverse().ToArray();
                        yield return new IPAddress(bytes);
                    }
                }
            }
            yield break;
        }

        // Single IP
        if (IPAddress.TryParse(range, out var single))
            yield return single;
    }
}
