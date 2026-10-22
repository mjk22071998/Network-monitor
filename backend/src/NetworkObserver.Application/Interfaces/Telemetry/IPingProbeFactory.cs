namespace NetworkObserver.Application.Interfaces.Telemetry;

/// <summary>
/// Factory pattern abstraction for instantiating ICMP ping probe strategies.
/// Complies with GoF Factory Pattern and DIP.
/// </summary>
public interface IPingProbeFactory
{
    IPingStrategy CreateStrategy();
}
