using Domain.Todos;

namespace Application.Todos.GetById;

public sealed class TodoResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    internal static TodoResponse From(TodoItem todoItem) => new()
    {
        Id = todoItem.Id,
        UserId = todoItem.UserId,
        Description = todoItem.Description,
        DueDate = todoItem.DueDate,
        Labels = [.. todoItem.Labels],
        IsCompleted = todoItem.IsCompleted,
        CreatedAt = todoItem.CreatedAt,
        CompletedAt = todoItem.CompletedAt,
        IsArchived = todoItem.IsArchived,
        ArchivedAt = todoItem.ArchivedAt
    };
}
