using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Todos.Get;

public sealed record GetTodosQuery(
    Guid UserId,
    string? Search,
    string? SortColumn,
    string? SortOrder,
    int Page,
    int PageSize,
    bool IncludeArchived) : IQuery<PagedList<TodoResponse>>;
