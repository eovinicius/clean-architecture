using Application.Abstractions.Authentication;
using Application.Todos.Archive;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class ArchiveTodoCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime FirstArchivedAt = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact] // SPEC-0001 AC-1
    public async Task Handle_Should_ArchiveTodoAndRaiseDomainEvent_WhenTodoIsCompleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, UserId, isCompleted: true, archivedAt: null);
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeTrue();
        todoItem.ArchivedAt.ShouldBe(Now);
        todoItem.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemArchivedDomainEvent);
    }

    [Fact] // SPEC-0001 AC-2
    public async Task Handle_Should_ReturnNotCompleted_WhenTodoIsNotCompleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, UserId, isCompleted: false, archivedAt: null);
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotCompleted");

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.ArchivedAt.ShouldBeNull();
    }

    [Fact] // SPEC-0001 AC-3
    public async Task Handle_Should_KeepOriginalArchivedAtAndRaiseNoEvent_WhenTodoIsAlreadyArchived()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, UserId, isCompleted: true, archivedAt: FirstArchivedAt);
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeTrue();
        todoItem.ArchivedAt.ShouldBe(FirstArchivedAt);
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact] // SPEC-0001 AC-14
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, Guid.NewGuid(), isCompleted: true, archivedAt: null);
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotFound");

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeFalse();
    }

    [Fact] // SPEC-0001 AC-14
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ArchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ArchiveTodoCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotFound");
    }

    private static ArchiveTodoCommandHandler CreateHandler(TestDbContext context)
    {
        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new ArchiveTodoCommandHandler(context, dateTimeProvider, userContext, cache);
    }

    private static async Task<Guid> SeedTodoAsync(TestDbContext context, Guid ownerId, bool isCompleted, DateTime? archivedAt)
    {
        var todoItem = TodoItem.Create(ownerId, "Existing todo", Priority.Low, null, [], DateTime.UtcNow);

        if (isCompleted)
        {
            todoItem.Complete(DateTime.UtcNow);
        }

        if (archivedAt is not null)
        {
            todoItem.Archive(archivedAt.Value);
        }

        todoItem.ClearDomainEvents();

        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        return todoItem.Id;
    }
}
