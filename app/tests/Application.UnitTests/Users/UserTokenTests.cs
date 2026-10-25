using Domain.Users;

namespace Application.UnitTests.Users;

public sealed class UserTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_Should_CreateUnusedTokenExpiringAfterPurposeLifetime()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var token = UserToken.Issue(userId, UserTokenPurpose.EmailConfirmation, "hash", Now);

        // Assert
        token.Id.ShouldNotBe(Guid.Empty);
        token.UserId.ShouldBe(userId);
        token.Purpose.ShouldBe(UserTokenPurpose.EmailConfirmation);
        token.TokenHash.ShouldBe("hash");
        token.CreatedAtUtc.ShouldBe(Now);
        token.ExpiresOnUtc.ShouldBe(Now.AddHours(24));
        token.UsedAtUtc.ShouldBeNull();
    }

    [Theory] // SPEC-0003 AC-4 (RN-3)
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsUsable_Should_AllowExactExpirationInstant_AndRejectAfterIt(long ticksAfterExpiration, bool expected)
    {
        // Arrange
        var token = UserToken.Issue(Guid.NewGuid(), UserTokenPurpose.EmailConfirmation, "hash", Now);

        // Act
        bool usable = token.IsUsable(Now.AddHours(24).AddTicks(ticksAfterExpiration));

        // Assert
        usable.ShouldBe(expected);
    }

    [Fact] // SPEC-0003 AC-4 (RN-4)
    public void IsUsable_Should_ReturnFalse_WhenTokenWasUsed()
    {
        // Arrange
        var token = UserToken.Issue(Guid.NewGuid(), UserTokenPurpose.EmailConfirmation, "hash", Now);

        // Act
        token.MarkUsed(Now.AddMinutes(1));

        // Assert
        token.UsedAtUtc.ShouldBe(Now.AddMinutes(1));
        token.IsUsable(Now.AddMinutes(2)).ShouldBeFalse();
    }

    [Theory] // SPEC-0004 AC-6 (RN-3)
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsUsable_Should_ExpirePasswordResetTokenAfterOneHour(long ticksAfterExpiration, bool expected)
    {
        // Arrange
        var token = UserToken.Issue(Guid.NewGuid(), UserTokenPurpose.PasswordReset, "hash", Now);

        // Act
        bool usable = token.IsUsable(Now.AddHours(1).AddTicks(ticksAfterExpiration));

        // Assert
        token.ExpiresOnUtc.ShouldBe(Now.AddHours(1));
        usable.ShouldBe(expected);
    }

    [Theory] // SPEC-0003 AC-11, AC-12 (RN-6)
    [InlineData(60, false)]
    [InlineData(61, true)]
    public void AllowsReissue_Should_RequireMoreThanSixtySeconds(int secondsAfterIssue, bool expected)
    {
        // Arrange
        var token = UserToken.Issue(Guid.NewGuid(), UserTokenPurpose.EmailConfirmation, "hash", Now);

        // Act
        bool allowsReissue = token.AllowsReissue(Now.AddSeconds(secondsAfterIssue));

        // Assert
        allowsReissue.ShouldBe(expected);
    }
}
