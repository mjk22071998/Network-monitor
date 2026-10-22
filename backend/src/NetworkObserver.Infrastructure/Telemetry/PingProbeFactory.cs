using NetworkObserver.Application.Interfaces.Telemetry;

namespace NetworkObserver.Infrastructure.Telemetry;

/// <summary>
/// Factory creating production raw socket ICMP probe strategies.
/// Complies with GoF Factory Pattern and DIP.
/// </summary>
public class PingProbeFactory : IPingProbeFactory
{
    public IPingStrategy CreateStrategy()
    {
        return new NativeSocketPingStrategy();
    }
}
