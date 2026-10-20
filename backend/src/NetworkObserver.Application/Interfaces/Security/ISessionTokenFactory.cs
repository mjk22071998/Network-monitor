namespace NetworkObserver.Application.Interfaces.Security;

/// <summary>
/// Factory pattern abstraction for producing high-entropy cryptographic session tokens.
/// Single Responsibility: Generates cryptographically secure 256-bit entropy tokens.
/// </summary>
public interface ISessionTokenFactory
{
    /// <summary>
    /// Produces a new 256-bit CSPRNG token formatted as 'obs_tok_{64-hex-characters}'.
    /// </summary>
    string GenerateToken();
}
