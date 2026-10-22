using System.Collections.Concurrent;
using NetworkObserver.Application.Interfaces.Telemetry;
using NetworkObserver.Domain.Entities;

namespace NetworkObserver.Application.Services;

/// <summary>
/// High-performance implementation of the Cascading Failure Suppression Circuit.
/// Maintains an in-memory switch-device DAG to detect parent outages and immediately
/// halt synthetic ICMP & NTP probes to downstream branches.
/// Eliminates network packet storms and industrial PLC lockups (FR-CAS-01 - FR-CAS-04).
/// </summary>
public class CascadeSuppressionCircuit : ICascadeSuppressionCircuit
{
    private const int MaxConcurrency = 50;

    public async Task<IReadOnlyList<ReachabilitySample>> ExecuteSweepAsync(
        IReadOnlyList<Device> monitoredDevices,
        int timeoutMs,
        bool enableSuppression,
        IPingStrategy pingStrategy,
        INtpStrategy ntpStrategy,
        DateTimeOffset timestamp,
        CancellationToken ct = default)
    {
        if (monitoredDevices.Count == 0)
        {
            return Array.Empty<ReachabilitySample>();
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrency,
            CancellationToken = ct
        };

        // If cascade suppression is disabled in monitoring_config, poll all devices directly
        if (!enableSuppression)
        {
            return await PollUnsuppressedAsync(monitoredDevices, timeoutMs, pingStrategy, ntpStrategy, timestamp, parallelOptions);
        }

        return await PollWithCascadeSuppressionAsync(monitoredDevices, timeoutMs, pingStrategy, ntpStrategy, timestamp, parallelOptions);
    }

    private static async Task<IReadOnlyList<ReachabilitySample>> PollUnsuppressedAsync(
        IReadOnlyList<Device> devices,
        int timeoutMs,
        IPingStrategy pingStrategy,
        INtpStrategy ntpStrategy,
        DateTimeOffset timestamp,
        ParallelOptions parallelOptions)
    {
        var samples = new ConcurrentBag<ReachabilitySample>();

        await Parallel.ForEachAsync(devices, parallelOptions, async (device, token) =>
        {
            var pingResult = await pingStrategy.PingAsync(device.PrimaryIp, timeoutMs, token);

            int? ntpOffsetUs = null;
            if (pingResult.IsSuccess)
            {
                var ntpResult = await ntpStrategy.QueryDriftAsync(device.PrimaryIp, 1000, token);
                if (ntpResult.IsSuccess)
                {
                    ntpOffsetUs = ntpResult.OffsetUs;
                }
            }

            var sample = new ReachabilitySample
            {
                DeviceId = device.DeviceId,
                RecordedAt = timestamp,
                StatusId = pingResult.IsSuccess ? (short)1 : (short)2,
                LatencyUs = pingResult.LatencyUs,
                LossPercentage = pingResult.LossPercentage,
                NtpOffsetUs = ntpOffsetUs,
                IsSuppressed = false
            };

            samples.Add(sample);
        });

        return samples.ToList();
    }

    private static async Task<IReadOnlyList<ReachabilitySample>> PollWithCascadeSuppressionAsync(
        IReadOnlyList<Device> monitoredDevices,
        int timeoutMs,
        IPingStrategy pingStrategy,
        INtpStrategy ntpStrategy,
        DateTimeOffset timestamp,
        ParallelOptions parallelOptions)
    {
        var deviceMap = monitoredDevices.ToDictionary(d => d.DeviceId);

        // Build parent-to-children dependency graph
        var childrenMap = new Dictionary<int, List<Device>>();
        foreach (var device in monitoredDevices)
        {
            if (device.ParentDeviceId.HasValue && deviceMap.ContainsKey(device.ParentDeviceId.Value))
            {
                var parentId = device.ParentDeviceId.Value;
                if (!childrenMap.TryGetValue(parentId, out var childList))
                {
                    childList = new List<Device>();
                    childrenMap[parentId] = childList;
                }
                childList.Add(device);
            }
        }

        var samples = new ConcurrentDictionary<int, ReachabilitySample>();
        var failedOrSuppressedParents = new ConcurrentDictionary<int, bool>();

        // Layer 0: Root devices (no parent device, or parent is outside monitored set)
        var currentLayer = monitoredDevices
            .Where(d => !d.ParentDeviceId.HasValue || !deviceMap.ContainsKey(d.ParentDeviceId.Value))
            .ToList();

        var visited = new HashSet<int>();

        while (currentLayer.Count > 0)
        {
            var toProbe = new List<Device>();
            var nextLayer = new List<Device>();

            foreach (var node in currentLayer)
            {
                if (!visited.Add(node.DeviceId))
                {
                    continue; // Prevent cyclic graph loop
                }

                // Check if any upstream parent has failed or been suppressed
                if (node.ParentDeviceId.HasValue && failedOrSuppressedParents.ContainsKey(node.ParentDeviceId.Value))
                {
                    // FR-CAS-02 & FR-CAS-03: Immediately mark SUPPRESSED without sending synthetic ICMP/NTP packets
                    var suppressedSample = new ReachabilitySample
                    {
                        DeviceId = node.DeviceId,
                        RecordedAt = timestamp,
                        StatusId = 3, // SUPPRESSED
                        LatencyUs = null, // Strict N/A Rule
                        LossPercentage = 100f,
                        NtpOffsetUs = null,
                        IsSuppressed = true
                    };

                    samples[node.DeviceId] = suppressedSample;
                    failedOrSuppressedParents[node.DeviceId] = true;
                }
                else
                {
                    toProbe.Add(node);
                }

                // Stage children for next layer traversal
                if (childrenMap.TryGetValue(node.DeviceId, out var children))
                {
                    nextLayer.AddRange(children);
                }
            }

            // Probe all eligible nodes at current layer in parallel
            if (toProbe.Count > 0)
            {
                await Parallel.ForEachAsync(toProbe, parallelOptions, async (device, token) =>
                {
                    var pingResult = await pingStrategy.PingAsync(device.PrimaryIp, timeoutMs, token);

                    int? ntpOffsetUs = null;
                    if (pingResult.IsSuccess)
                    {
                        var ntpResult = await ntpStrategy.QueryDriftAsync(device.PrimaryIp, 1000, token);
                        if (ntpResult.IsSuccess)
                        {
                            ntpOffsetUs = ntpResult.OffsetUs;
                        }
                    }

                    var statusId = pingResult.IsSuccess ? (short)1 : (short)2; // 1 = REACHABLE, 2 = UNREACHABLE

                    var sample = new ReachabilitySample
                    {
                        DeviceId = device.DeviceId,
                        RecordedAt = timestamp,
                        StatusId = statusId,
                        LatencyUs = pingResult.LatencyUs,
                        LossPercentage = pingResult.LossPercentage,
                        NtpOffsetUs = ntpOffsetUs,
                        IsSuppressed = false
                    };

                    samples[device.DeviceId] = sample;

                    // If node failed, flag it as failed parent to suppress all downstream descendants
                    if (!pingResult.IsSuccess)
                    {
                        failedOrSuppressedParents[device.DeviceId] = true;
                    }
                });
            }

            currentLayer = nextLayer;
        }

        // Catch any disconnected/unvisited monitored devices (failsafe against disconnected graph cycles)
        var unvisited = monitoredDevices.Where(d => !visited.Contains(d.DeviceId)).ToList();
        if (unvisited.Count > 0)
        {
            await Parallel.ForEachAsync(unvisited, parallelOptions, async (device, token) =>
            {
                var pingResult = await pingStrategy.PingAsync(device.PrimaryIp, timeoutMs, token);
                var sample = new ReachabilitySample
                {
                    DeviceId = device.DeviceId,
                    RecordedAt = timestamp,
                    StatusId = pingResult.IsSuccess ? (short)1 : (short)2,
                    LatencyUs = pingResult.LatencyUs,
                    LossPercentage = pingResult.LossPercentage,
                    NtpOffsetUs = null,
                    IsSuppressed = false
                };
                samples[device.DeviceId] = sample;
            });
        }

        return samples.Values.ToList();
    }
}
