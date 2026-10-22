using NetworkObserver.Application.Interfaces.Discovery;

namespace NetworkObserver.Infrastructure.Discovery;

/// <summary>
/// OUI prefix heuristic vendor identifier for enterprise campus and lab hardware.
/// </summary>
public class MacVendorLookup : IMacVendorLookup
{
    private static readonly Dictionary<string, string> OuiMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Cisco Systems (Campus Backbone & Access Switches)
        ["00:00:0C"] = "Cisco",
        ["00:01:42"] = "Cisco",
        ["00:1A:2B"] = "Cisco",
        ["00:24:14"] = "Cisco",
        ["50:3D:E5"] = "Cisco",
        ["F8:C0:01"] = "Cisco",
        
        // Dell (University Lab Workstations & PowerEdge Servers)
        ["00:14:22"] = "Dell",
        ["B8:2A:72"] = "Dell",
        ["18:66:DA"] = "Dell",
        ["D4:BE:D9"] = "Dell",
        ["54:BF:64"] = "Dell",
        ["74:86:7A"] = "Dell",

        // HP / Hewlett Packard Enterprise (Lab Workstations & ProLiant Servers)
        ["00:1E:0B"] = "HP",
        ["3C:D9:2B"] = "HP",
        ["9C:8E:99"] = "HP",
        ["70:5A:0F"] = "HP",
        ["D4:C9:EF"] = "HP",

        // Lenovo (ThinkCentre Lab Workstations)
        ["00:21:CC"] = "Lenovo",
        ["54:EE:75"] = "Lenovo",
        ["8C:16:45"] = "Lenovo",
        ["E8:6A:64"] = "Lenovo",

        // Apple (Mac Studio / Mac Mini Design & Research Labs)
        ["00:17:F2"] = "Apple",
        ["F0:18:98"] = "Apple",
        ["AC:BC:32"] = "Apple",
        ["38:F9:D3"] = "Apple",
        ["A4:83:E7"] = "Apple",

        // Intel (NUCs & Server NICs)
        ["00:1B:21"] = "Intel",
        ["00:1E:67"] = "Intel",
        ["68:05:CA"] = "Intel",
        ["A4:4C:C8"] = "Intel",

        // Juniper Networks & Arista (Campus Core / Distribution)
        ["00:10:DB"] = "Juniper",
        ["00:1C:73"] = "Arista",

        // Virtualized Lab Hypervisors (VMware ESXi, Hyper-V, KVM)
        ["00:05:69"] = "VMware",
        ["00:0C:29"] = "VMware",
        ["00:50:56"] = "VMware",
        ["00:15:5D"] = "Microsoft Hyper-V",
        ["52:54:00"] = "QEMU / KVM",

        // Embedded / Robotics Lab Computing
        ["B8:27:EB"] = "Raspberry Pi",
        ["DC:A6:32"] = "Raspberry Pi",
        ["E4:5F:01"] = "Raspberry Pi"
    };

    public string? LookupVendor(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress) || macAddress.Length < 8)
        {
            return "Unknown Vendor";
        }

        var prefix = macAddress[..8];
        return OuiMap.TryGetValue(prefix, out var vendor) ? vendor : "Unknown Vendor";
    }
}
