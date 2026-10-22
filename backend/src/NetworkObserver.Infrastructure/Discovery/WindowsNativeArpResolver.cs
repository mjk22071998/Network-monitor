using System.Net;
using System.Runtime.InteropServices;
using NetworkObserver.Application.Interfaces.Discovery;

namespace NetworkObserver.Infrastructure.Discovery;

/// <summary>
/// Native Windows IP Helper ARP protocol resolver using P/Invoke SendARP.
/// Provides zero-dependency hardware MAC address queries across local broadcast domains.
/// Cross-platform guarded for non-Windows environments.
/// </summary>
public class WindowsNativeArpResolver : IArpResolver
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] pMacAddr, ref int phyAddrLen);

    public Task<string?> ResolveMacAddressAsync(IPAddress ipAddress, CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var ipBytes = ipAddress.GetAddressBytes();
            if (ipBytes.Length != 4)
            {
                return Task.FromResult<string?>(null);
            }

            int destIp = BitConverter.ToInt32(ipBytes, 0);
            byte[] macBytes = new byte[6];
            int macLen = macBytes.Length;

            int status = SendARP(destIp, 0, macBytes, ref macLen);
            if (status == 0 && macLen == 6)
            {
                string mac = string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}",
                    macBytes[0], macBytes[1], macBytes[2], macBytes[3], macBytes[4], macBytes[5]);
                return Task.FromResult<string?>(mac);
            }
        }
        catch
        {
            // Suppress P/Invoke failure and return null safely
        }

        return Task.FromResult<string?>(null);
    }
}
