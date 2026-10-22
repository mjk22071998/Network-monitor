using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using NetworkObserver.Application.Interfaces.Telemetry;

namespace NetworkObserver.Infrastructure.Telemetry;

/// <summary>
/// Native asynchronous 48-byte UDP NTP client (RFC 4330 / RFC 5905) querying remote devices on UDP port 123.
/// Strictly bounded allocations, zero subprocesses, non-blocking asynchronous socket I/O.
/// Computes microsecond clock drift (ntp_offset_us) and strictly records NULL on failure or timeout.
/// </summary>
public class UdpSocketNtpStrategy : INtpStrategy
{
    private const int NtpPort = 123;
    private const int NtpPacketSize = 48;
    private static readonly DateTimeOffset NtpEpoch = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task<NtpProbeResult> QueryDriftAsync(string ipAddress, int timeoutMs = 1000, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var targetIp))
        {
            return new NtpProbeResult(false, null, "InvalidIpAddress");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            using var socket = new Socket(targetIp.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            // Ignore WSAECONNRESET on Windows when ICMP Port Unreachable packet arrives
            if (OperatingSystem.IsWindows())
            {
                const int SIO_UDP_CONNRESET = -1744830452; // 0x9800000C
                byte[] inValue = [0];
                byte[] outValue = [];
                socket.IOControl(SIO_UDP_CONNRESET, inValue, outValue);
            }

            var endPoint = new IPEndPoint(targetIp, NtpPort);

            // Construct 48-byte SNTP Client Request Datagram
            byte[] buffer = new byte[NtpPacketSize];
            // LI = 0 (no warning), VN = 4 (IPv4/v6), Mode = 3 (Client) -> 0x23
            buffer[0] = 0x23;

            var t1 = DateTimeOffset.UtcNow;
            WriteNtpTimestamp(buffer.AsSpan(40, 8), t1);

            await socket.SendToAsync(buffer.AsMemory(), SocketFlags.None, endPoint, cts.Token);

            EndPoint senderRemote = new IPEndPoint(targetIp.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            var receiveResult = await socket.ReceiveFromAsync(buffer.AsMemory(), SocketFlags.None, senderRemote, cts.Token);
            var t4 = DateTimeOffset.UtcNow;

            if (receiveResult.ReceivedBytes < NtpPacketSize)
            {
                return new NtpProbeResult(false, null, "TruncatedPacket");
            }

            // Extract timestamps from response
            var t2 = ReadNtpTimestamp(buffer.AsSpan(32, 8)); // Receive Timestamp (Server)
            var t3 = ReadNtpTimestamp(buffer.AsSpan(40, 8)); // Transmit Timestamp (Server)

            if (t2 == DateTimeOffset.MinValue || t3 == DateTimeOffset.MinValue)
            {
                return new NtpProbeResult(false, null, "UnsynchronizedServer");
            }

            // SNTP Clock Offset: theta = ((T2 - T1) + (T3 - T4)) / 2
            var t2MinusT1 = (t2 - t1).TotalMilliseconds * 1000.0;
            var t3MinusT4 = (t3 - t4).TotalMilliseconds * 1000.0;
            var offsetUsDouble = (t2MinusT1 + t3MinusT4) / 2.0;

            if (offsetUsDouble > int.MaxValue || offsetUsDouble < int.MinValue)
            {
                return new NtpProbeResult(false, null, "OffsetOverflow");
            }

            int offsetUs = (int)Math.Round(offsetUsDouble);
            return new NtpProbeResult(true, offsetUs);
        }
        catch (OperationCanceledException)
        {
            return new NtpProbeResult(false, null, "TimedOut");
        }
        catch (SocketException ex)
        {
            return new NtpProbeResult(false, null, $"SocketError_{ex.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            return new NtpProbeResult(false, null, ex.Message);
        }
    }

    private static DateTimeOffset ReadNtpTimestamp(ReadOnlySpan<byte> span)
    {
        uint intPart = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(0, 4));
        uint fracPart = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(4, 4));

        if (intPart == 0 && fracPart == 0)
        {
            return DateTimeOffset.MinValue;
        }

        double milliseconds = (intPart * 1000.0) + ((fracPart * 1000.0) / 0x100000000L);
        return NtpEpoch.AddMilliseconds(milliseconds);
    }

    private static void WriteNtpTimestamp(Span<byte> span, DateTimeOffset time)
    {
        var totalSeconds = (time - NtpEpoch).TotalSeconds;
        if (totalSeconds < 0) totalSeconds = 0;

        uint intPart = (uint)totalSeconds;
        uint fracPart = (uint)((totalSeconds - intPart) * 0x100000000L);

        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(0, 4), intPart);
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(4, 4), fracPart);
    }
}
