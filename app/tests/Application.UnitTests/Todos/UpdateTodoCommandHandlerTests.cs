using Application.Abstractions.Authentication;
using Application.Todos.Update;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class UpdateTodoCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoBelongsToAnotherUser()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: Guid.NewGuid());

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotFound");
    }

    [Fact]
    public async Task Handle_Should_UpdateDescriptionAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: UserId);

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.Description.ShouldBe("Updated description");
        todoItem.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemUpdatedDomainEvent);
    }

    [Fact] // SPEC-0001 AC-6
    public async Task Handle_Should_ReturnArchived_WhenTodoIsArchived()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, ownerId: UserId, isArchived: true);

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new UpdateTodoCommandHandler(context, userContext, cache);
        var command = new UpdateTodoCommand(todoItemId, "Updated description");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.Archived");

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.Description.ShouldBe("Original description");
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    private static async Task<Guid> SeedTodoAsync(TestDbContext context, Guid ownerId, bool isArchived = false)
    {
        var todoItem = TodoItem.Create(ownerId, "Original description", Priority.Low, null, [], DateTime.UtcNow);

        if (isArchived)
        {
            todoItem.Complete(DateTime.UtcNow);
            todoItem.Archive(DateTime.UtcNow);
        }

        todoItem.ClearDomainEvents();

        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        return todoItem.Id;
    }
}
