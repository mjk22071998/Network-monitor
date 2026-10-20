using System.Security.Cryptography;
using NetworkObserver.Application.Interfaces.Security;

namespace NetworkObserver.Infrastructure.Security;

/// <summary>
/// Factory pattern implementation generating 256-bit cryptographically secure pseudorandom session tokens.
/// </summary>
public class SessionTokenFactory : ISessionTokenFactory
{
    private const string TokenPrefix = "obs_tok_";

    public string GenerateToken()
    {
        // 32 bytes = 256 bits of true entropy
        Span<byte> randomBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return $"{TokenPrefix}{Convert.ToHexString(randomBytes).ToLowerInvariant()}";
    }
}
