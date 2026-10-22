using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NetworkObserver.Application.Interfaces.Telemetry;

namespace NetworkObserver.Infrastructure.Telemetry;

/// <summary>
/// Strategy implementation for querying host operating health from endpoint agents on port 9182.
/// Evaluates CPU, RAM, network throughput, and multi-volume disk partitions.
/// Enforces Strict N/A Rule (nulls on failure) and bounded memory.
/// </summary>
public class HttpAgentHealthStrategy : IHostHealthStrategy
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectTimeout = TimeSpan.FromSeconds(3)
    })
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<HostHealthProbeResult> QueryHealthAsync(
        string ipAddress,
        short deviceTypeId,
        int timeoutMs = 3000,
        CancellationToken ct = default)
    {
        // Network infrastructure devices (Routers/Switches) do not host an OS endpoint agent
        if (deviceTypeId is 1 or 2 or 3)
        {
            return new HostHealthProbeResult(
                IsSuccess: false,
                StatusId: 4, // UNSUPPORTED
                CpuUtilizationPct: null,
                RamUtilizationPct: null,
                NetRxKbps: null,
                NetTxKbps: null,
                TemperatureCelsius: null,
                DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                ErrorMessage: "UnsupportedEquipmentTier"
            );
        }

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return new HostHealthProbeResult(
                IsSuccess: false,
                StatusId: 2, // UNAVAILABLE
                CpuUtilizationPct: null,
                RamUtilizationPct: null,
                NetRxKbps: null,
                NetTxKbps: null,
                TemperatureCelsius: null,
                DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                ErrorMessage: "InvalidIpAddress"
            );
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            var url = $"http://{ipAddress}:9182/health";
            var response = await HttpClient.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new HostHealthProbeResult(
                    IsSuccess: false,
                    StatusId: 2, // UNAVAILABLE
                    CpuUtilizationPct: null,
                    RamUtilizationPct: null,
                    NetRxKbps: null,
                    NetTxKbps: null,
                    TemperatureCelsius: null,
                    DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                    ErrorMessage: $"HttpStatus_{response.StatusCode}"
                );
            }

            var payload = await response.Content.ReadFromJsonAsync<AgentHealthPayload>(cancellationToken: cts.Token);
            if (payload == null)
            {
                return new HostHealthProbeResult(
                    IsSuccess: false,
                    StatusId: 2,
                    CpuUtilizationPct: null,
                    RamUtilizationPct: null,
                    NetRxKbps: null,
                    NetTxKbps: null,
                    TemperatureCelsius: null,
                    DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                    ErrorMessage: "EmptyPayload"
                );
            }

            var diskVolumes = (payload.DiskVolumes ?? new List<AgentDiskVolume>())
                .Select(d => new DiskVolumeProbeResult(
                    VolumeName: d.VolumeName ?? "C:",
                    UsedPercentage: d.UsedPercentage,
                    TotalCapacityGb: d.TotalCapacityGb,
                    FreeSpaceGb: d.FreeSpaceGb
                ))
                .ToList();

            return new HostHealthProbeResult(
                IsSuccess: true,
                StatusId: 1, // AVAILABLE
                CpuUtilizationPct: payload.CpuUtilizationPct,
                RamUtilizationPct: payload.RamUtilizationPct,
                NetRxKbps: payload.NetRxKbps,
                NetTxKbps: payload.NetTxKbps,
                TemperatureCelsius: payload.TemperatureCelsius,
                DiskVolumes: diskVolumes
            );
        }
        catch (OperationCanceledException)
        {
            return new HostHealthProbeResult(
                IsSuccess: false,
                StatusId: 2, // UNAVAILABLE
                CpuUtilizationPct: null,
                RamUtilizationPct: null,
                NetRxKbps: null,
                NetTxKbps: null,
                TemperatureCelsius: null,
                DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                ErrorMessage: "TimedOut"
            );
        }
        catch (HttpRequestException ex)
        {
            return new HostHealthProbeResult(
                IsSuccess: false,
                StatusId: 2, // UNAVAILABLE
                CpuUtilizationPct: null,
                RamUtilizationPct: null,
                NetRxKbps: null,
                NetTxKbps: null,
                TemperatureCelsius: null,
                DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                ErrorMessage: ex.Message
            );
        }
        catch (Exception ex)
        {
            return new HostHealthProbeResult(
                IsSuccess: false,
                StatusId: 5, // ERROR
                CpuUtilizationPct: null,
                RamUtilizationPct: null,
                NetRxKbps: null,
                NetTxKbps: null,
                TemperatureCelsius: null,
                DiskVolumes: Array.Empty<DiskVolumeProbeResult>(),
                ErrorMessage: ex.Message
            );
        }
    }

    private class AgentHealthPayload
    {
        [JsonPropertyName("cpu_utilization_pct")]
        public float? CpuUtilizationPct { get; set; }

        [JsonPropertyName("ram_utilization_pct")]
        public float? RamUtilizationPct { get; set; }

        [JsonPropertyName("net_rx_kbps")]
        public float? NetRxKbps { get; set; }

        [JsonPropertyName("net_tx_kbps")]
        public float? NetTxKbps { get; set; }

        [JsonPropertyName("temperature_celsius")]
        public float? TemperatureCelsius { get; set; }

        [JsonPropertyName("disk_volumes")]
        public List<AgentDiskVolume>? DiskVolumes { get; set; }
    }

    private class AgentDiskVolume
    {
        [JsonPropertyName("volume_name")]
        public string? VolumeName { get; set; }

        [JsonPropertyName("used_percentage")]
        public float UsedPercentage { get; set; }

        [JsonPropertyName("total_capacity_gb")]
        public float TotalCapacityGb { get; set; }

        [JsonPropertyName("free_space_gb")]
        public float FreeSpaceGb { get; set; }
    }
}
