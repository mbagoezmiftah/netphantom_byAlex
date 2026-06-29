using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NetPhantom.Helpers;

/// <summary>
/// Lightweight OUI (MAC vendor) lookup.
/// Reads an embedded oui.txt next to the exe, or falls back to a small built-in table.
/// </summary>
public static class OuiLookup
{
    private static readonly Dictionary<string, string> _table = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static string Lookup(string macAddress)
    {
        if (!_loaded) Load();

        if (string.IsNullOrWhiteSpace(macAddress)) return "Unknown";

        // Normalise: take first 3 octets, strip separators
        var clean = macAddress.Replace(":", "").Replace("-", "").Replace(".", "");
        if (clean.Length < 6) return "Unknown";
        var oui = clean[..6].ToUpperInvariant();

        return _table.TryGetValue(oui, out var vendor) ? vendor : "Unknown";
    }

    private static void Load()
    {
        _loaded = true;
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        var path = Path.Combine(dir, "oui.txt");

        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path))
            {
                // Format: "001122\tVendor Name"
                if (line.Length > 6 && line[6] == '\t')
                    _table[line[..6]] = line[7..].Trim();
            }
            return;
        }

        // Minimal built-in fallback
        foreach (var (k, v) in BuiltinTable)
            _table[k] = v;
    }

    private static readonly Dictionary<string, string> BuiltinTable = new()
    {
        { "000000", "Xerox" },
        { "001122", "Cimsys" },
        { "00005E", "IANA" },
        { "001A2B", "Cisco" },
        { "0050F2", "Microsoft" },
        { "001C42", "Parallels" },
        { "080027", "VirtualBox" },
        { "000C29", "VMware" },
        { "005056", "VMware" },
        { "DC:A6:32", "Raspberry Pi" },
        { "B8:27:EB", "Raspberry Pi" },
        { "00:1B:21", "Intel" },
        { "8C:85:90", "Intel" },
    };
}
