using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;
using NetworkObserver.Application.Interfaces.Security;

namespace NetworkObserver.Infrastructure.Security;

/// <summary>
/// Single Responsibility: Salt generation, Argon2id hashing, and constant-time verification adhering to RFC 9106.
/// Parameters: m=65536 (64 MB), t=3 passes, p=4 threads, 16-byte CSPRNG salt, 32-byte key length.
/// </summary>
public class Argon2idPasswordHasher : IPasswordHasher
{
    // Pre-computed dummy hash used to equalize execution time on non-existent or inactive user queries
    private static readonly string DummyHashString = 
        "$argon2id$v=19$m=65536,t=3,p=4$vO7T0n0tZlP6yEa+W0qL0A$kC78vPZ35gV1s1fB0uK7n9tQ6dE3aL1zP5yE8vR4m0w";

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);

        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing, // Argon2id
            Version = Argon2Version.Nineteen,
            TimeCost = 3,
            MemoryCost = 65536, // 64 MB (KiB)
            Lanes = 4,
            Threads = 4,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = salt.ToArray(),
            HashLength = 32
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return Argon2.Verify(passwordHash, password);
        }
        catch
        {
            return false;
        }
    }

    public void DummyHash()
    {
        // Executes a full verification pass against a static dummy hash to defeat timing side-channel detection
        try
        {
            Argon2.Verify(DummyHashString, "DummyPasswordForTimingMitigation123!");
        }
        catch
        {
            // Suppress any errors during dummy calculation
        }
    }
}
