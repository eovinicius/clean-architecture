using Domain.Users;

namespace Application.UnitTests.Users;

public sealed class UserTests
{
    [Fact]
    public void Create_Should_InitializeUserAndRaiseRegisteredEvent()
    {
        // Act
        var user = User.Create("test@example.com", "Test", "User", "hash");

        // Assert
        user.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe("test@example.com");
        user.FirstName.ShouldBe("Test");
        user.LastName.ShouldBe("User");
        user.PasswordHash.ShouldBe("hash");

        UserRegisteredDomainEvent domainEvent = user.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<UserRegisteredDomainEvent>();
        domainEvent.UserId.ShouldBe(user.Id);
        domainEvent.Email.ShouldBe(user.Email);
    }
}
