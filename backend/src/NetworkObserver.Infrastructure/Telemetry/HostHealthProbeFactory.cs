using NetworkObserver.Application.Interfaces.Telemetry;

namespace NetworkObserver.Infrastructure.Telemetry;

/// <summary>
/// Factory pattern implementation generating IHostHealthStrategy instances.
/// Complies with GoF Factory Pattern and DIP.
/// </summary>
public class HostHealthProbeFactory : IHostHealthProbeFactory
{
    public IHostHealthStrategy CreateStrategy()
    {
        return new HttpAgentHealthStrategy();
    }
}
