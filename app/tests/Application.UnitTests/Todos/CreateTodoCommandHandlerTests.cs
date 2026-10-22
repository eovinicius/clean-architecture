using Application.Abstractions.Authentication;
using Application.Todos.Create;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class CreateTodoCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUserIdDoesNotMatchContext()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CreateTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CreateTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(userId));
    }

    [Fact]
    public async Task Handle_Should_PersistTodoAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("test@example.com", "Test", "User", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CreateTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(user.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == result.Value);
        todoItem.Description.ShouldBe("Write unit tests");
        todoItem.UserId.ShouldBe(user.Id);
        todoItem.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemCreatedDomainEvent);
    }

    private static CreateTodoCommand CreateCommand(Guid userId) => new()
    {
        UserId = userId,
        Description = "Write unit tests",
        Priority = Priority.Medium,
        Labels = ["work"]
    };
}
