using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NetPhantom.Models;
using NetPhantom.Services;
using Serilog;
using SharpPcap;

namespace NetPhantom.ViewModels;

public partial class ScannerViewModel : ObservableObject
{
    // ── Events ────────────────────────────────────────────────────────────────
    /// Fired when the user double-clicks a host row; MainViewModel wires this.
    public event Action<HostInfo>? HostSelected;

    // ── State ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<NetworkInterfaceInfo> _interfaces = new();
    [ObservableProperty] private NetworkInterfaceInfo? _selectedInterface;
    [ObservableProperty] private string _scanRange = "192.168.1.0/24";
    [ObservableProperty] private bool _useArpScan = true;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private ObservableCollection<HostInfo> _hosts = new();
    [ObservableProperty] private ObservableCollection<string> _logLines = new();

    private CancellationTokenSource? _cts;
    private readonly ISnackbarMessageQueue _snackbar;

    public ScannerViewModel(AppSettings settings, ISnackbarMessageQueue snackbar)
    {
        _snackbar = snackbar;
        ScanRange = settings.DefaultScanRange;
        UseArpScan = settings.UseArpScan;
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
            Log.Error(ex, "LoadInterfaces failed");
        }
    }

    [RelayCommand]
    private void RefreshInterfaces() => LoadInterfaces();

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task StartScanAsync()
    {
        if (SelectedInterface == null) return;

        Hosts.Clear();
        LogLines.Clear();
        IsScanning = true;
        ProgressValue = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<HostInfo>(host =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Hosts.Add(host);
                    AddLog($"发现: {host.IpAddress}  {host.MacAddress}  {host.Vendor}");
                });
            });

            StatusText = "扫描中...";

            if (UseArpScan)
            {
                var pcapDevice = GetPcapDevice(SelectedInterface.DeviceName);
                if (pcapDevice == null)
                {
                    _snackbar.Enqueue("未找到对应网卡的 Pcap 设备，请安装 Npcap");
                    return;
                }

                var scanner = new ArpScannerService(pcapDevice);
                await scanner.ScanArpAsync(
                    ScanRange,
                    SelectedInterface.MacAddress,
                    SelectedInterface.IpAddress,
                    progress,
                    _cts.Token);
            }
            else
            {
                await new ArpScannerService(null!).ScanPingAsync(
                    ScanRange, progress, _cts.Token);
            }

            StatusText = $"完成，共发现 {Hosts.Count} 台主机";
            AddLog($"扫描完成，共 {Hosts.Count} 台在线主机");
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
            AddLog("扫描已取消");
        }
        catch (Exception ex)
        {
            StatusText = $"错误: {ex.Message}";
            AddLog($"扫描错误: {ex.Message}");
            Log.Error(ex, "Scan failed");
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanStartScan() => !IsScanning && SelectedInterface != null;

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void CancelScan()
    {
        _cts?.Cancel();
        AddLog("正在取消...");
    }

    [RelayCommand]
    private void SelectHost(HostInfo? host)
    {
        if (host != null)
            HostSelected?.Invoke(host);
    }

    private void AddLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        App.Current.Dispatcher.Invoke(() => LogLines.Insert(0, line));
        Log.Information(message);
    }

    private static ILiveDevice? GetPcapDevice(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName)) return null;
        return CaptureDeviceList.Instance
            .FirstOrDefault(d => d.Name == deviceName);
    }
}
