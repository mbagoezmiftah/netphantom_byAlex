namespace NetPhantom.Models;

public class AppSettings
{
    // Scanner defaults
    public string DefaultScanRange { get; set; } = "192.168.1.0/24";
    public bool UseArpScan { get; set; } = true;
    public int ScanTimeoutMs { get; set; } = 2000;
    public int MaxConcurrency { get; set; } = 50;

    // Spoofer defaults
    public int SpoofIntervalMs { get; set; } = 1000;
    public bool AutoDetectGateway { get; set; } = true;

    // UI preferences
    public string Theme { get; set; } = "Dark";
    public bool ShowLogPanel { get; set; } = true;
    public bool ConfirmBeforeSpoof { get; set; } = true;
}
