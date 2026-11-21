using Application.Abstractions.Pagination;
using Domain.Todos;

namespace Application.Abstractions.Data;

public interface ITodoItemRepository
{
    // Ownership filter: another user's todo is reported as missing.
    Task<TodoItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<PagedList<TodoItem>> GetPagedAsync(TodoItemListFilter filter, CancellationToken cancellationToken = default);

    void Add(TodoItem todoItem);

    void Remove(TodoItem todoItem);
}
