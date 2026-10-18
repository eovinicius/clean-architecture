using SharedKernel;

namespace Domain.Todos;

public static class TodoItemErrors
{
    public static Error AlreadyCompleted(Guid todoItemId) => Error.Problem(
        "TodoItems.AlreadyCompleted",
        $"The todo item with Id = '{todoItemId}' is already completed.");

    public static Error Archived(Guid todoItemId) => Error.Problem(
        "TodoItems.Archived",
        $"The todo item with Id = '{todoItemId}' is archived and cannot be changed.");

    public static Error NotCompleted(Guid todoItemId) => Error.Problem(
        "TodoItems.NotCompleted",
        $"The todo item with Id = '{todoItemId}' must be completed before it can be archived.");

    public static Error NotFound(Guid todoItemId) => Error.NotFound(
        "TodoItems.NotFound",
        $"The to-do item with the Id = '{todoItemId}' was not found");
}
