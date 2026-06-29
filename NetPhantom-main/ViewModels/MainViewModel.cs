using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using NetPhantom.Services;

namespace NetPhantom.ViewModels;

public partial class MainViewModel : ObservableObject
{
    // ── Sub-ViewModels ────────────────────────────────────────────────────────
    public ScannerViewModel ScannerVm { get; }
    public SpooferViewModel SpooferVm { get; }
    public SettingsViewModel SettingsVm { get; }

    // ── Navigation ────────────────────────────────────────────────────────────
    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private bool _isDrawerOpen;
    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string _currentPageTitle = "主机扫描";

    // ── Snackbar ──────────────────────────────────────────────────────────────
    public ISnackbarMessageQueue MessageQueue { get; } = new SnackbarMessageQueue();

    private readonly SettingsService _settingsService = new();

    public MainViewModel()
    {
        var settings = _settingsService.Load();
        ScannerVm = new ScannerViewModel(settings, MessageQueue);
        SpooferVm = new SpooferViewModel(settings, MessageQueue);
        SettingsVm = new SettingsViewModel(settings, _settingsService, MessageQueue);

        // Wire: scanner host double-click → pre-fill spoofer
        // EventHandler<HostInfo> wajib 2 parameter (sender, args) — sebelumnya cuma 1.
        ScannerVm.HostSelected += (sender, host) =>
        {
            SpooferVm.TargetIp = host.IpAddress;
            SpooferVm.TargetMac = host.MacAddress;
            SelectedIndex = 1;   // switch to Spoofer tab
        };

        NavigateTo(0);
    }

    partial void OnSelectedIndexChanged(int value) => NavigateTo(value);

    private void NavigateTo(int index)
    {
        (CurrentView, CurrentPageTitle) = index switch
        {
            0 => ((object)ScannerVm,  "主机扫描"),
            1 => (SpooferVm,          "ARP 欺骗"),
            2 => (SettingsVm,         "设置"),
            _ => (ScannerVm,          "主机扫描")
        };
    }

    [RelayCommand]
    private void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;
}
