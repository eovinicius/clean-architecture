using Domain.Users;
using Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using SharedKernel;

namespace IntegrationTests.Users;

public sealed class TokenProviderTests
{
    private static readonly JwtOptions JwtOptions = new()
    {
        Secret = "super-duper-secret-value-that-should-be-in-user-secrets",
        Issuer = "clean-architecture",
        Audience = "developers",
        ExpirationInMinutes = 60,
        RefreshTokenExpirationInDays = 7
    };

    // Truncated to whole seconds: JWT timestamps have second precision.
    private static readonly DateTime FixedUtcNow = DateTime.UnixEpoch.AddSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private readonly TokenProvider _tokenProvider = new(Options.Create(JwtOptions), new FixedDateTimeProvider(FixedUtcNow));

    [Fact]
    public void Create_Should_IssueTokenWithUserClaimsIssuerAndAudience()
    {
        // Arrange
        var user = User.Create("test@example.com", "Test", "User", "hash");

        // Act
        JsonWebToken token = new JsonWebTokenHandler().ReadJsonWebToken(_tokenProvider.Create(user));

        // Assert
        token.Subject.ShouldBe(user.Id.ToString());
        token.GetClaim(JwtRegisteredClaimNames.Email).Value.ShouldBe(user.Email);
        token.Issuer.ShouldBe(JwtOptions.Issuer);
        token.Audiences.ShouldBe([JwtOptions.Audience]);
    }

    [Fact]
    public void Create_Should_ExpireAfterConfiguredMinutes()
    {
        // Arrange
        var user = User.Create("test@example.com", "Test", "User", "hash");

        // Act
        JsonWebToken token = new JsonWebTokenHandler().ReadJsonWebToken(_tokenProvider.Create(user));

        // Assert
        token.ValidTo.ShouldBe(FixedUtcNow.AddMinutes(JwtOptions.ExpirationInMinutes));
    }

    [Fact]
    public void RefreshTokenLifetime_Should_ComeFromOptions()
    {
        // Act
        TimeSpan lifetime = _tokenProvider.RefreshTokenLifetime;

        // Assert
        lifetime.ShouldBe(TimeSpan.FromDays(JwtOptions.RefreshTokenExpirationInDays));
    }

    [Fact]
    public void GenerateRefreshToken_Should_Carry32RandomBytes()
    {
        // Act
        string first = _tokenProvider.GenerateRefreshToken();
        string second = _tokenProvider.GenerateRefreshToken();

        // Assert
        Convert.FromBase64String(first).Length.ShouldBe(32);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void HashRefreshToken_Should_BeDeterministicSha256Hex()
    {
        // Arrange
        string refreshToken = _tokenProvider.GenerateRefreshToken();

        // Act
        string hash = _tokenProvider.HashRefreshToken(refreshToken);

        // Assert
        hash.ShouldBe(_tokenProvider.HashRefreshToken(refreshToken));
        hash.Length.ShouldBe(64);
        hash.ShouldAllBe(c => Uri.IsHexDigit(c));
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
