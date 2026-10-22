namespace NetworkObserver.Application.Interfaces.Telemetry;

/// <summary>
/// Factory pattern abstraction for producing IHostHealthStrategy instances.
/// Complies with GoF Factory Pattern and DIP.
/// </summary>
public interface IHostHealthProbeFactory
{
    IHostHealthStrategy CreateStrategy();
}
