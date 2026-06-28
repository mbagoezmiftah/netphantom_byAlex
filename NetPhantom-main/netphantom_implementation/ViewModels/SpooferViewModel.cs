using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NetPhantom.Models;
using NetPhantom.Services;
using Serilog;
using SharpPcap;

namespace NetPhantom.ViewModels;

public partial class SpooferViewModel : ObservableObject
{
    // ── Interface list ────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<NetworkInterfaceInfo> _interfaces = new();
    [ObservableProperty] private NetworkInterfaceInfo? _selectedInterface;

    // ── Target fields ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _targetIp = string.Empty;
    [ObservableProperty] private string _targetMac = string.Empty;
    [ObservableProperty] private string _gatewayIp = string.Empty;
    [ObservableProperty] private int _intervalMs = 1000;

    // ── Runtime state ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSpoofing;
    [ObservableProperty] private int _packetsSent;
    [ObservableProperty] private string _lastSentAt = "—";
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private ObservableCollection<string> _logLines = new();

    private ArpSpoofService? _service;
    private readonly ISnackbarMessageQueue _snackbar;
    private readonly bool _confirmBeforeSpoof;

    public SpooferViewModel(AppSettings settings, ISnackbarMessageQueue snackbar)
    {
        _snackbar = snackbar;
        _confirmBeforeSpoof = settings.ConfirmBeforeSpoof;
        IntervalMs = settings.SpoofIntervalMs;
        LoadInterfaces();
    }

    private void LoadInterfaces()
    {
        try
        {
            var list = NetworkHelper.GetInterfaces();
            Interfaces = new ObservableCollection<NetworkInterfaceInfo>(list);
            SelectedInterface = list.FirstOrDefault();
        }
        catch (Exception ex)
        {
            AddLog($"加载网卡失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DetectGateway()
    {
        if (SelectedInterface == null) return;
        var gw = NetworkHelper.GetDefaultGateway(SelectedInterface.IpAddress);
        if (gw != null)
        {
            GatewayIp = gw;
            _snackbar.Enqueue($"网关已自动检测: {gw}");
        }
        else
        {
            _snackbar.Enqueue("无法自动检测网关，请手动输入");
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartSpoof()
    {
        if (SelectedInterface == null) return;

        var pcapDevice = CaptureDeviceList.Instance
            .FirstOrDefault(d => d.Name == SelectedInterface.DeviceName);

        if (pcapDevice == null)
        {
            _snackbar.Enqueue("未找到 Pcap 设备，请安装 Npcap 并以管理员身份运行");
            return;
        }

        _service = new ArpSpoofService(pcapDevice);
        _service.LogMessage += msg =>
            App.Current.Dispatcher.Invoke(() =>
            {
                LogLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
                PacketsSent = _service.PacketsSent;
                LastSentAt = _service.LastSentAt?.ToString("HH:mm:ss") ?? "—";
            });

        try
        {
            _service.Start(
                SelectedInterface.MacAddress,
                TargetIp, TargetMac,
                GatewayIp,
                IntervalMs);

            IsSpoofing = true;
            StatusText = $"欺骗中: {TargetIp} ↔ {GatewayIp}";
            Log.Information("Spoof started against {Target}", TargetIp);
        }
        catch (Exception ex)
        {
            _snackbar.Enqueue($"启动失败: {ex.Message}");
            Log.Error(ex, "StartSpoof failed");
        }
    }

    private bool CanStart() =>
        !IsSpoofing &&
        !string.IsNullOrWhiteSpace(TargetIp) &&
        !string.IsNullOrWhiteSpace(TargetMac) &&
        !string.IsNullOrWhiteSpace(GatewayIp);

    [RelayCommand(CanExecute = nameof(IsSpoofing))]
    private async Task StopSpoofAsync()
    {
        if (_service == null) return;
        await _service.StopAsync();
        _service.Dispose();
        _service = null;
        IsSpoofing = false;
        StatusText = "已停止";
    }

    private void AddLog(string msg) =>
        App.Current.Dispatcher.Invoke(() => LogLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}"));
}
