using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.GetCurrent;
using Domain.Users;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests : BaseHandlerTest
{
    [Fact] // SPEC-0002 AC-1
    public async Task Handle_Should_ReturnCurrentUser_WhenUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("current@example.com", "Current", "User", "hash", DateTime.UnixEpoch);
        context.Users.Add(user);
        context.Users.Add(User.Create("other@example.com", "Other", "Person", "hash", DateTime.UnixEpoch));
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);

        var handler = new GetCurrentUserQueryHandler(context, userContext);

        // Act
        Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe("current@example.com");
        result.Value.FirstName.ShouldBe("Current");
        result.Value.LastName.ShouldBe("User");
    }

    [Fact] // SPEC-0002 AC-4
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());

        var handler = new GetCurrentUserQueryHandler(context, userContext);

        // Act
        Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFound");
    }
}
