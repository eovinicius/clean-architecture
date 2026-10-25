using Domain.Users;

namespace Application.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_InitializeUserAndRaiseRegisteredEvent()
    {
        // Act
        var user = User.Create("test@example.com", "Test", "User", "hash", Now);

        // Assert
        user.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe("test@example.com");
        user.FirstName.ShouldBe("Test");
        user.LastName.ShouldBe("User");
        user.PasswordHash.ShouldBe("hash");
        user.RegisteredAt.ShouldBe(Now);

        UserRegisteredDomainEvent domainEvent = user.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<UserRegisteredDomainEvent>();
        domainEvent.UserId.ShouldBe(user.Id);
        domainEvent.Email.ShouldBe(user.Email);
    }

    [Fact] // SPEC-0003 AC-1
    public void Create_Should_LeaveEmailUnconfirmed()
    {
        // Act
        var user = User.Create("test@example.com", "Test", "User", "hash", Now);

        // Assert
        user.EmailConfirmedAt.ShouldBeNull();
    }

    [Fact] // SPEC-0003 AC-3
    public void ConfirmEmail_Should_SetConfirmationInstantAndRaiseEvent_WhenUnconfirmed()
    {
        // Arrange
        User user = CreateUser();
        user.ClearDomainEvents();

        // Act
        user.ConfirmEmail(Now.AddHours(1));

        // Assert
        user.EmailConfirmedAt.ShouldBe(Now.AddHours(1));
        UserEmailConfirmedDomainEvent domainEvent = user.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<UserEmailConfirmedDomainEvent>();
        domainEvent.UserId.ShouldBe(user.Id);
    }

    [Fact] // SPEC-0003 AC-7
    public void ConfirmEmail_Should_KeepOriginalInstantAndRaiseNothing_WhenAlreadyConfirmed()
    {
        // Arrange
        User user = CreateUser();
        user.ConfirmEmail(Now.AddHours(1));
        user.ClearDomainEvents();

        // Act
        user.ConfirmEmail(Now.AddHours(2));

        // Assert
        user.EmailConfirmedAt.ShouldBe(Now.AddHours(1));
        user.DomainEvents.ShouldBeEmpty();
    }

    [Theory] // SPEC-0003 AC-18
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void IsUnconfirmedRegistrationExpired_Should_SelectUnconfirmedUser_OnlyAfterSevenDays(
        long ticksAfterSevenDays,
        bool expected)
    {
        // Arrange
        User user = CreateUser();
        Func<User, bool> isExpired = User
            .IsUnconfirmedRegistrationExpired(Now.AddDays(7).AddTicks(ticksAfterSevenDays))
            .Compile();

        // Act
        bool selected = isExpired(user);

        // Assert
        selected.ShouldBe(expected);
    }

    [Fact] // SPEC-0003 AC-19
    public void IsUnconfirmedRegistrationExpired_Should_NotSelectConfirmedUser()
    {
        // Arrange
        User user = CreateUser();
        user.ConfirmEmail(Now.AddDays(1));
        Func<User, bool> isExpired = User.IsUnconfirmedRegistrationExpired(Now.AddDays(30)).Compile();

        // Act
        bool selected = isExpired(user);

        // Assert
        selected.ShouldBeFalse();
    }

    private static User CreateUser() => User.Create("test@example.com", "Test", "User", "hash", Now);
}
