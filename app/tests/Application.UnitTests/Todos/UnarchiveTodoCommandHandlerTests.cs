using Application.Abstractions.Authentication;
using Application.Todos.Unarchive;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class UnarchiveTodoCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact] // SPEC-0001 AC-4
    public async Task Handle_Should_UnarchiveTodoAndRaiseDomainEvent_WhenTodoIsArchived()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, UserId, isArchived: true);
        UnarchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnarchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.ArchivedAt.ShouldBeNull();
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemUnarchivedDomainEvent);
    }

    [Fact] // SPEC-0001 AC-5
    public async Task Handle_Should_ChangeNothingAndRaiseNoEvent_WhenTodoIsNotArchived()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, UserId, isArchived: false);
        UnarchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnarchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.ArchivedAt.ShouldBeNull();
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact] // SPEC-0001 AC-14
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, Guid.NewGuid(), isArchived: true);
        UnarchiveTodoCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnarchiveTodoCommand(todoItemId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotFound");

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsArchived.ShouldBeTrue();
    }

    private static UnarchiveTodoCommandHandler CreateHandler(TestDbContext context)
    {
        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        return new UnarchiveTodoCommandHandler(context, userContext, cache);
    }

    private static async Task<Guid> SeedTodoAsync(TestDbContext context, Guid ownerId, bool isArchived)
    {
        var todoItem = TodoItem.Create(ownerId, "Existing todo", Priority.Low, null, [], DateTime.UtcNow);
        todoItem.Complete(DateTime.UtcNow);

        if (isArchived)
        {
            todoItem.Archive(DateTime.UtcNow);
        }

        todoItem.ClearDomainEvents();

        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        return todoItem.Id;
    }
}
