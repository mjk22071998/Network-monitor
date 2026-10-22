using System.Net;

namespace NetworkObserver.Application.Interfaces.Discovery;

/// <summary>
/// Abstraction for IP-to-MAC resolution via native OS ARP protocols.
/// Complies with GoF Strategy Pattern and DIP.
/// </summary>
public interface IArpResolver
{
    Task<string?> ResolveMacAddressAsync(IPAddress ipAddress, CancellationToken ct = default);
}
