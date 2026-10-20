using NetworkObserver.Infrastructure.Security;
using Xunit;
using Xunit.Abstractions;

namespace NetworkObserver.Tests.Security;

public class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _hasher = new();
    private readonly ITestOutputHelper _output;

    public Argon2idPasswordHasherTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HashPassword_ShouldGenerateValidPhcFormat()
    {
        // Arrange
        const string password = "TestPassword2026!";

        // Act
        var hash = _hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=4$", hash);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        const string password = "CorrectHorseBatteryStaple123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var isValid = _hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        const string password = "CorrectPassword123!";
        const string wrongPassword = "WrongPassword123!";
        var hash = _hasher.HashPassword(password);

        // Act
        var isValid = _hasher.VerifyPassword(wrongPassword, hash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyPassword_WithPreSeededAdminHash_ShouldReturnTrue()
    {
        // Arrange - Cryptographically valid Argon2id hash from sql/001_initial_schema.sql
        const string preSeededHash = "$argon2id$v=19$m=65536,t=3,p=4$0l6lAQpfBHCuwjnZvBYFwA$r7tpyUj2Tqun7gzxcnPy/5wIurBEMj7NnvzDZG9Pueg";
        const string password = "Admin@NetworkObserver2026!";

        // Act
        var isValid = _hasher.VerifyPassword(password, preSeededHash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void DummyHash_ShouldExecuteWithoutException()
    {
        // Act & Assert (Should execute full Argon2id calculation without throwing)
        var exception = Record.Exception(() => _hasher.DummyHash());
        Assert.Null(exception);
    }
}
