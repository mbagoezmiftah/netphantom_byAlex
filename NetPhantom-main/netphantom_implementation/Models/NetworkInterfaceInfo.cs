namespace NetPhantom.Models;

public class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;   // SharpPcap device name

    public override string ToString() =>
        string.IsNullOrEmpty(IpAddress)
            ? $"{Description} (no IP)"
            : $"{Description} ({IpAddress})";
}
