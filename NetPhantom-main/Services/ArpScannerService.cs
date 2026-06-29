using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NetPhantom.Helpers;
using NetPhantom.Models;
using PacketDotNet;
using Serilog;
using SharpPcap;

namespace NetPhantom.Services;

public class ArpScannerService
{
    private readonly ILiveDevice? _device;

    public ArpScannerService(ILiveDevice? device) { _device = device; }

    public async Task ScanArpAsync(
        string range, string localMac, string localIp,
        IProgress<HostInfo> progress, CancellationToken ct,
        int timeoutMs = 2000, int maxConcurrency = 50)
    {
        if (_device == null) return;
        var targets = NetworkHelper.ExpandRange(range).ToList();
        var sem = new SemaphoreSlim(maxConcurrency);
        var captured = new System.Collections.Concurrent.ConcurrentDictionary<string, (string Mac, long RttMs)>();
        var tasks = new List<Task>();

        _device.Open(DeviceModes.Promiscuous, timeoutMs);
        _device.OnPacketArrival += (_, e) =>
        {
            try
            {
                var pkt = Packet.ParsePacket(e.GetPacket().LinkLayerType, e.GetPacket().Data);
                if (pkt?.PayloadPacket is ArpPacket arp && arp.Operation == ArpOperation.Response)
                {
                    var ip = arp.SenderProtocolAddress.ToString();
                    var mac = string.Join(":", arp.SenderHardwareAddress.GetAddressBytes()
                        .Select(b => b.ToString("X2")));
                    captured[ip] = (mac, 0);
                }
            }
            catch { }
        };
        _device.StartCapture();

        foreach (var ip in targets)
        {
            if (ct.IsCancellationRequested) break;
            await sem.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    SendArpRequest(_device, localMac, localIp, ip.ToString());
                    await Task.Delay(timeoutMs, ct);
                    if (captured.TryGetValue(ip.ToString(), out var info))
                    {
                        progress.Report(new HostInfo
                        {
                            IpAddress = ip.ToString(),
                            MacAddress = info.Mac,
                            Vendor = OuiLookup.Lookup(info.Mac),
                            ResponseTimeMs = info.RttMs,
                            IsOnline = true
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Warning(ex, "ARP scan error for {Ip}", ip); }
                finally { sem.Release(); }
            }, ct));
        }

        await Task.WhenAll(tasks);
        try { _device.StopCapture(); _device.Close(); } catch { }
    }

    private static void SendArpRequest(ILiveDevice device, string localMac, string localIp, string targetIp)
    {
        var localMacBytes = ParseMac(localMac);
        var broadcast = new PhysicalAddress(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
        var localPhysical = new PhysicalAddress(localMacBytes);
        var arp = new ArpPacket(ArpOperation.Request, broadcast,
            IPAddress.Parse(targetIp), localPhysical, IPAddress.Parse(localIp));
        var eth = new EthernetPacket(localPhysical, broadcast, EthernetType.Arp) { PayloadPacket = arp };
        device.SendPacket(eth);
    }

    public async Task ScanPingAsync(
        string range, IProgress<HostInfo> progress, CancellationToken ct,
        int timeoutMs = 2000, int maxConcurrency = 50)
    {
        var targets = NetworkHelper.ExpandRange(range).ToList();
        var sem = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>();

        foreach (var ip in targets)
        {
            if (ct.IsCancellationRequested) break;
            await sem.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        progress.Report(new HostInfo
                        {
                            IpAddress = ip.ToString(),
                            MacAddress = "N/A",
                            Vendor = "N/A",
                            HostName = await TryResolveHostName(ip.ToString()),
                            ResponseTimeMs = reply.RoundtripTime,
                            IsOnline = true
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Warning(ex, "Ping error for {Ip}", ip); }
                finally { sem.Release(); }
            }, ct));
        }
        await Task.WhenAll(tasks);
    }

    private static async Task<string> TryResolveHostName(string ip)
    {
        try { return (await Dns.GetHostEntryAsync(ip)).HostName; }
        catch { return string.Empty; }
    }

    private static byte[] ParseMac(string mac) =>
        mac.Split(':', '-').Select(p => Convert.ToByte(p, 16)).ToArray();
}
