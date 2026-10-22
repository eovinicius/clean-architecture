using SharedKernel;

namespace Domain.Todos;

public sealed class TodoItem : Entity
{
    private TodoItem()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Description { get; private set; }
    public DateTime? DueDate { get; private set; }
    public List<string> Labels { get; private set; } = [];
    public bool IsCompleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Priority Priority { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

    public static TodoItem Create(
        Guid userId,
        string description,
        Priority priority,
        DateTime? dueDate,
        IEnumerable<string> labels,
        DateTime createdAt)
    {
        var todoItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            Labels = [.. labels],
            CreatedAt = createdAt
        };

        todoItem.Raise(new TodoItemCreatedDomainEvent(todoItem.Id));

        return todoItem;
    }

    // The copy starts over: it is neither completed nor archived.
    public TodoItem Copy(DateTime createdAt) =>
        Create(UserId, Description, Priority, DueDate, Labels, createdAt);

    public Result Complete(DateTime completedAt)
    {
        // SPEC-0001 RN-5: arquivada tem precedência sobre concluída, embora toda tarefa arquivada esteja concluída.
        if (IsArchived)
        {
            return Result.Failure(TodoItemErrors.Archived(Id));
        }

        if (IsCompleted)
        {
            return Result.Failure(TodoItemErrors.AlreadyCompleted(Id));
        }

        IsCompleted = true;
        CompletedAt = completedAt;

        Raise(new TodoItemCompletedDomainEvent(Id));

        return Result.Success();
    }

    public Result Archive(DateTime archivedAt)
    {
        // SPEC-0001 RN-4: arquivar é idempotente e preserva o ArchivedAt original.
        if (IsArchived)
        {
            return Result.Success();
        }

        if (!IsCompleted)
        {
            return Result.Failure(TodoItemErrors.NotCompleted(Id));
        }

        IsArchived = true;
        ArchivedAt = archivedAt;

        Raise(new TodoItemArchivedDomainEvent(Id));

        return Result.Success();
    }

    public void Unarchive()
    {
        // SPEC-0001 RN-4: desarquivar uma tarefa não arquivada não altera nada.
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
        ArchivedAt = null;

        Raise(new TodoItemUnarchivedDomainEvent(Id));
    }

    public Result UpdateDescription(string description)
    {
        if (IsArchived)
        {
            return Result.Failure(TodoItemErrors.Archived(Id));
        }

        Description = description;

        Raise(new TodoItemUpdatedDomainEvent(Id));

        return Result.Success();
    }

    public void Delete() => Raise(new TodoItemDeletedDomainEvent(Id));
}
