namespace NetworkObserver.Application.Interfaces.Security;

/// <summary>
/// Cryptographic password hashing and verification abstraction adhering to RFC 9106 Argon2id standard.
/// Single Responsibility: Provide constant-time password verification and irreversible salted hashing.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using Argon2id with 16-byte random salt, m=64MB, t=3, p=4.
    /// Returns standard PHC string format ($argon2id$v=19$m=65536,t=3,p=4$...).
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a PHC Argon2id hash in constant time.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// Performs a dummy Argon2id verification pass with equal computational cost
    /// to mitigate timing side-channel attacks on deactivated or non-existent accounts.
    /// </summary>
    void DummyHash();
}
