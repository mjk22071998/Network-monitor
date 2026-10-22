using System.Diagnostics;
using System.Net.NetworkInformation;
using NetworkObserver.Application.Interfaces.Telemetry;

namespace NetworkObserver.Infrastructure.Telemetry;

/// <summary>
/// Native asynchronous raw ICMP socket strategy using System.Net.NetworkInformation.Ping.
/// Zero subprocess spawning; high-resolution microsecond latency measurement.
/// Enforces Strict N/A Rule: LatencyUs is null on failure/timeout.
/// </summary>
public class NativeSocketPingStrategy : IPingStrategy
{
    private static readonly byte[] PingBuffer = new byte[32]; // Standard 32-byte payload

    public async Task<PingProbeResult> PingAsync(string ipAddress, int timeoutMs, CancellationToken ct = default)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeoutMs, PingBuffer);
            var elapsed = Stopwatch.GetElapsedTime(start);

            if (reply.Status == IPStatus.Success)
            {
                var latencyUs = (int)(elapsed.TotalMilliseconds * 1000.0);
                return new PingProbeResult(
                    IsSuccess: true,
                    LatencyUs: Math.Max(1, latencyUs),
                    LossPercentage: 0.0f
                );
            }

            // Strict N/A Rule: Latency is NULL on failure/timeout (never 0)
            return new PingProbeResult(
                IsSuccess: false,
                LatencyUs: null,
                LossPercentage: 100.0f,
                ErrorMessage: reply.Status.ToString()
            );
        }
        catch (Exception ex)
        {
            return new PingProbeResult(
                IsSuccess: false,
                LatencyUs: null,
                LossPercentage: 100.0f,
                ErrorMessage: ex.Message
            );
        }
    }
}
