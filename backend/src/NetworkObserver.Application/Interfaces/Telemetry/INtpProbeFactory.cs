namespace NetworkObserver.Application.Interfaces.Telemetry;

/// <summary>
/// Factory pattern abstraction for producing INtpStrategy instances.
/// Complies with GoF Factory Pattern and DIP.
/// </summary>
public interface INtpProbeFactory
{
    INtpStrategy CreateStrategy();
}
