using NetworkObserver.Application.Interfaces.Telemetry;

namespace NetworkObserver.Infrastructure.Telemetry;

/// <summary>
/// Factory pattern implementation generating INtpStrategy instances for NTP telemetry.
/// Complies with GoF Factory Pattern and DIP.
/// </summary>
public class NtpProbeFactory : INtpProbeFactory
{
    public INtpStrategy CreateStrategy()
    {
        return new UdpSocketNtpStrategy();
    }
}
