using Domain.Users;

namespace Application.UnitTests.Users;

public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_Should_CreateActiveTokenInGivenFamily()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        // Act
        var refreshToken = RefreshToken.Issue(userId, "hash", familyId, Now.AddDays(7));

        // Assert
        refreshToken.Id.ShouldNotBe(Guid.Empty);
        refreshToken.UserId.ShouldBe(userId);
        refreshToken.FamilyId.ShouldBe(familyId);
        refreshToken.TokenHash.ShouldBe("hash");
        refreshToken.ExpiresOnUtc.ShouldBe(Now.AddDays(7));
        refreshToken.IsRevoked.ShouldBeFalse();
        refreshToken.RevokedAtUtc.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void IsExpired_Should_CompareExpirationWithNow(int expiresInSeconds, bool expected)
    {
        // Arrange
        var refreshToken = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now.AddSeconds(expiresInSeconds));

        // Act
        bool isExpired = refreshToken.IsExpired(Now);

        // Assert
        isExpired.ShouldBe(expected);
    }

    [Fact]
    public void Revoke_Should_RecordRevocationTime()
    {
        // Arrange
        var refreshToken = RefreshToken.Issue(Guid.NewGuid(), "hash", Guid.NewGuid(), Now.AddDays(7));

        // Act
        refreshToken.Revoke(Now);

        // Assert
        refreshToken.IsRevoked.ShouldBeTrue();
        refreshToken.RevokedAtUtc.ShouldBe(Now);
    }
}
