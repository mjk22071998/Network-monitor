namespace NetworkObserver.Application.Interfaces.Discovery;

/// <summary>
/// Abstraction for IEEE MAC OUI hardware vendor identification heuristics.
/// </summary>
public interface IMacVendorLookup
{
    string? LookupVendor(string macAddress);
}
