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
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TodoId = Guid.NewGuid();

    private static CopyTodoCommand Command => new()
    {
        UserId = UserId,
        TodoId = TodoId
    };

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
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(UserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(TodoId));
    }

    [Fact]
    public async Task Handle_Should_PersistCopyWithNewIdAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        context.TodoItems.Add(new TodoItem
        {
            Id = TodoId,
            UserId = UserId,
            Description = "Original",
            Priority = Priority.High,
            Labels = ["work"],
            IsCompleted = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CopyTodoCommandHandler(context, dateTimeProvider, userContext);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        result.Value.ShouldNotBe(TodoId);

        TodoItem copy = await context.TodoItems.SingleAsync(t => t.Id == result.Value);
        copy.Description.ShouldBe("Original");
        copy.IsCompleted.ShouldBeFalse();
        copy.DomainEvents.OfType<TodoItemCreatedDomainEvent>()
            .ShouldHaveSingleItem()
            .TodoItemId.ShouldBe(result.Value);
    }

    private static async Task SeedUserAsync(TestDbContext context)
    {
        context.Users.Add(new User
        {
            Id = UserId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        });
        await context.SaveChangesAsync();
    }
}
