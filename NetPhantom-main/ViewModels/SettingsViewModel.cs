using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NetPhantom.Models;
using NetPhantom.Services;

namespace NetPhantom.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly ISnackbarMessageQueue _snackbar;

    // ── Scanner ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _defaultScanRange = "192.168.1.0/24";
    [ObservableProperty] private bool _useArpScan = true;
    [ObservableProperty] private int _scanTimeoutMs = 2000;
    [ObservableProperty] private int _maxConcurrency = 50;

    // ── Spoofer ───────────────────────────────────────────────────────────────
    [ObservableProperty] private int _spoofIntervalMs = 1000;
    [ObservableProperty] private bool _autoDetectGateway = true;

    // ── UI ────────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _showLogPanel = true;
    [ObservableProperty] private bool _confirmBeforeSpoof = true;

    public SettingsViewModel(AppSettings settings, SettingsService service, ISnackbarMessageQueue snackbar)
    {
        _service = service;
        _snackbar = snackbar;
        LoadFrom(settings);
    }

    private void LoadFrom(AppSettings s)
    {
        DefaultScanRange = s.DefaultScanRange;
        UseArpScan = s.UseArpScan;
        ScanTimeoutMs = s.ScanTimeoutMs;
        MaxConcurrency = s.MaxConcurrency;
        SpoofIntervalMs = s.SpoofIntervalMs;
        AutoDetectGateway = s.AutoDetectGateway;
        ShowLogPanel = s.ShowLogPanel;
        ConfirmBeforeSpoof = s.ConfirmBeforeSpoof;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            DefaultScanRange = DefaultScanRange,
            UseArpScan = UseArpScan,
            ScanTimeoutMs = ScanTimeoutMs,
            MaxConcurrency = MaxConcurrency,
            SpoofIntervalMs = SpoofIntervalMs,
            AutoDetectGateway = AutoDetectGateway,
            ShowLogPanel = ShowLogPanel,
            ConfirmBeforeSpoof = ConfirmBeforeSpoof,
        };
        _service.Save(settings);
        _snackbar.Enqueue("设置已保存 ✓");
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        LoadFrom(new AppSettings());
        _snackbar.Enqueue("已恢复默认设置");
    }
}
