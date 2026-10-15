using System.Security.Cryptography;
using Infrastructure.Authentication;

namespace IntegrationTests.Users;

// Pure tests of the Infrastructure hasher; no database or web host needed.
public sealed class PasswordHasherTests
{
    private const string Password = "Password123";

    private readonly PasswordHasher _passwordHasher = new();

    [Fact]
    public void Hash_Should_ProduceVersionedHash_ThatVerifies()
    {
        // Act
        string hash = _passwordHasher.Hash(Password);

        // Assert
        hash.ShouldStartWith("v1$500000$");
        _passwordHasher.Verify(Password, hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_Should_ReturnFalse_WhenPasswordIsWrong()
    {
        // Arrange
        string hash = _passwordHasher.Hash(Password);

        // Act
        bool verified = _passwordHasher.Verify("WrongPassword1", hash);

        // Assert
        verified.ShouldBeFalse();
    }

    [Fact]
    public void Verify_Should_AcceptLegacyFormat()
    {
        // Arrange
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Password, salt, 500000, HashAlgorithmName.SHA512, 32);
        string legacyHash = $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";

        // Act
        bool verified = _passwordHasher.Verify(Password, legacyHash);

        // Assert
        verified.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v9$1$AA$BB")]
    [InlineData("v1$abc$AA$BB")]
    [InlineData("v1$1000$ZZ$BB")]
    [InlineData("AA")]
    public void Verify_Should_ReturnFalse_WhenHashIsMalformed(string malformedHash)
    {
        // Act
        bool verified = _passwordHasher.Verify(Password, malformedHash);

        // Assert
        verified.ShouldBeFalse();
    }
}
