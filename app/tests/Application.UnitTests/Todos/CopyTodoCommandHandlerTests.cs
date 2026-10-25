using Application.Abstractions.Authentication;
using Application.Todos.Copy;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class CopyTodoCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenUserIdDoesNotMatchContext()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

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

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(userId, Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(userId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context);
        var todoId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(userId, todoId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(todoId));
    }

    [Fact]
    public async Task Handle_Should_PersistCopyWithNewIdAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context);
        var original = TodoItem.Create(userId, "Original", Priority.High, null, ["work"], DateTime.UtcNow);
        original.Complete(DateTime.UtcNow);
        context.TodoItems.Add(original);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(CreateCommand(userId, original.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        result.Value.ShouldNotBe(original.Id);

        TodoItem copy = await context.TodoItems.SingleAsync(t => t.Id == result.Value);
        copy.Description.ShouldBe("Original");
        copy.Labels.ShouldBe(["work"]);
        copy.IsCompleted.ShouldBeFalse();
        copy.DomainEvents.OfType<TodoItemCreatedDomainEvent>()
            .ShouldHaveSingleItem()
            .TodoItemId.ShouldBe(result.Value);
    }

    private static CopyTodoCommand CreateCommand(Guid userId, Guid todoId) => new()
    {
        UserId = userId,
        TodoId = todoId
    };

    private static async Task<Guid> SeedUserAsync(TestDbContext context)
    {
        var user = User.Create("test@example.com", "Test", "User", "hash", DateTime.UnixEpoch);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }
}
