using Domain.Todos;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class TodoItemTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime CreatedAt = new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_InitializePendingTodoAndRaiseCreatedEvent()
    {
        // Arrange
        List<string> labels = ["work"];

        // Act
        var todoItem = TodoItem.Create(UserId, "Write tests", Priority.High, Now.AddDays(1), labels, CreatedAt);

        // Assert
        todoItem.Id.ShouldNotBe(Guid.Empty);
        todoItem.UserId.ShouldBe(UserId);
        todoItem.Description.ShouldBe("Write tests");
        todoItem.Priority.ShouldBe(Priority.High);
        todoItem.DueDate.ShouldBe(Now.AddDays(1));
        todoItem.Labels.ShouldBe(["work"]);
        todoItem.Labels.ShouldNotBeSameAs(labels);
        todoItem.CreatedAt.ShouldBe(CreatedAt);
        todoItem.IsCompleted.ShouldBeFalse();
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<TodoItemCreatedDomainEvent>()
            .TodoItemId.ShouldBe(todoItem.Id);
    }

    [Fact]
    public void Copy_Should_CreatePendingTodoWithSameContentAndNewId()
    {
        // Arrange
        TodoItem original = CreateArchivedTodo();

        // Act
        TodoItem copy = original.Copy(Now);

        // Assert
        copy.Id.ShouldNotBe(original.Id);
        copy.UserId.ShouldBe(original.UserId);
        copy.Description.ShouldBe(original.Description);
        copy.Priority.ShouldBe(original.Priority);
        copy.DueDate.ShouldBe(original.DueDate);
        copy.Labels.ShouldBe(original.Labels);
        copy.Labels.ShouldNotBeSameAs(original.Labels);
        copy.CreatedAt.ShouldBe(Now);
        copy.IsCompleted.ShouldBeFalse();
        copy.CompletedAt.ShouldBeNull();
        copy.IsArchived.ShouldBeFalse();
        copy.ArchivedAt.ShouldBeNull();
        copy.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TodoItemCreatedDomainEvent>();
    }

    [Fact]
    public void Complete_Should_MarkAsCompletedAndRaiseEvent_WhenPending()
    {
        // Arrange
        TodoItem todoItem = CreatePendingTodo();

        // Act
        Result result = todoItem.Complete(Now);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.CompletedAt.ShouldBe(Now);
        todoItem.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TodoItemCompletedDomainEvent>();
    }

    [Fact]
    public void Complete_Should_ReturnAlreadyCompleted_WhenCompleted()
    {
        // Arrange
        TodoItem todoItem = CreateCompletedTodo();

        // Act
        Result result = todoItem.Complete(Now);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.AlreadyCompleted(todoItem.Id));
        todoItem.CompletedAt.ShouldBe(CreatedAt);
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact] // SPEC-0001 RN-5
    public void Complete_Should_ReturnArchived_WhenArchived()
    {
        // Arrange
        TodoItem todoItem = CreateArchivedTodo();

        // Act
        Result result = todoItem.Complete(Now);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.Archived(todoItem.Id));
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Archive_Should_ArchiveAndRaiseEvent_WhenCompleted()
    {
        // Arrange
        TodoItem todoItem = CreateCompletedTodo();

        // Act
        Result result = todoItem.Archive(Now);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.IsArchived.ShouldBeTrue();
        todoItem.ArchivedAt.ShouldBe(Now);
        todoItem.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TodoItemArchivedDomainEvent>();
    }

    [Fact]
    public void Archive_Should_ReturnNotCompleted_WhenPending()
    {
        // Arrange
        TodoItem todoItem = CreatePendingTodo();

        // Act
        Result result = todoItem.Archive(Now);

        // Assert
        result.Error.ShouldBe(TodoItemErrors.NotCompleted(todoItem.Id));
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.ArchivedAt.ShouldBeNull();
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact] // SPEC-0001 RN-4
    public void Archive_Should_KeepOriginalArchivedAtAndRaiseNoEvent_WhenAlreadyArchived()
    {
        // Arrange
        TodoItem todoItem = CreateArchivedTodo();

        // Act
        Result result = todoItem.Archive(Now);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.ArchivedAt.ShouldBe(CreatedAt);
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Unarchive_Should_UnarchiveAndRaiseEvent_WhenArchived()
    {
        // Arrange
        TodoItem todoItem = CreateArchivedTodo();

        // Act
        todoItem.Unarchive();

        // Assert
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.ArchivedAt.ShouldBeNull();
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TodoItemUnarchivedDomainEvent>();
    }

    [Fact] // SPEC-0001 RN-4
    public void Unarchive_Should_ChangeNothing_WhenNotArchived()
    {
        // Arrange
        TodoItem todoItem = CreateCompletedTodo();

        // Act
        todoItem.Unarchive();

        // Assert
        todoItem.IsArchived.ShouldBeFalse();
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateDescription_Should_ChangeDescriptionAndRaiseEvent_WhenNotArchived()
    {
        // Arrange
        TodoItem todoItem = CreatePendingTodo();

        // Act
        Result result = todoItem.UpdateDescription("New description");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        todoItem.Description.ShouldBe("New description");
        todoItem.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<TodoItemUpdatedDomainEvent>();
    }

    [Fact]
    public void UpdateDescription_Should_ReturnArchived_WhenArchived()
    {
        // Arrange
        TodoItem todoItem = CreateArchivedTodo();

        // Act
        Result result = todoItem.UpdateDescription("New description");

        // Assert
        result.Error.ShouldBe(TodoItemErrors.Archived(todoItem.Id));
        todoItem.Description.ShouldBe("Original");
        todoItem.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Delete_Should_RaiseDeletedEvent()
    {
        // Arrange
        TodoItem todoItem = CreatePendingTodo();

        // Act
        todoItem.Delete();

        // Assert
        todoItem.DomainEvents.ShouldHaveSingleItem()
            .ShouldBeOfType<TodoItemDeletedDomainEvent>()
            .TodoItemId.ShouldBe(todoItem.Id);
    }

    private static TodoItem CreatePendingTodo()
    {
        var todoItem = TodoItem.Create(UserId, "Original", Priority.Low, Now.AddDays(2), ["home"], CreatedAt);
        todoItem.ClearDomainEvents();

        return todoItem;
    }

    private static TodoItem CreateCompletedTodo()
    {
        TodoItem todoItem = CreatePendingTodo();
        todoItem.Complete(CreatedAt);
        todoItem.ClearDomainEvents();

        return todoItem;
    }

    private static TodoItem CreateArchivedTodo()
    {
        TodoItem todoItem = CreateCompletedTodo();
        todoItem.Archive(CreatedAt);
        todoItem.ClearDomainEvents();

        return todoItem;
    }
}
