using CommunityToolkit.Mvvm.ComponentModel;

namespace NetPhantom.Models;

public partial class HostInfo : ObservableObject
{
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private string _macAddress = string.Empty;
    [ObservableProperty] private string _vendor = string.Empty;
    [ObservableProperty] private string _hostName = string.Empty;
    [ObservableProperty] private long _responseTimeMs;
    [ObservableProperty] private bool _isOnline;
}
