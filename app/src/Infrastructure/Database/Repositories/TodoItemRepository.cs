using System.Linq.Expressions;
using Application.Abstractions.Data;
using Application.Abstractions.Pagination;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

internal sealed class TodoItemRepository(ApplicationDbContext context) : ITodoItemRepository
{
    private const string LikeEscapeCharacter = @"\";

    public Task<TodoItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        context.TodoItems.SingleOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

    public Task<PagedList<TodoItem>> GetPagedAsync(TodoItemListFilter filter, CancellationToken cancellationToken = default)
    {
        IQueryable<TodoItem> query = context.TodoItems
            .AsNoTracking()
            .Where(todoItem => todoItem.UserId == filter.UserId);

        if (!filter.IncludeArchived)
        {
            query = query.Where(todoItem => !todoItem.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string pattern = $"%{EscapeLikePattern(filter.Search)}%";

            query = query.Where(todoItem => EF.Functions.ILike(todoItem.Description, pattern, LikeEscapeCharacter));
        }

        query = string.Equals(filter.SortOrder, "desc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(GetSortProperty(filter.SortColumn))
            : query.OrderBy(GetSortProperty(filter.SortColumn));

        return query.ToPagedListAsync(filter.Page, filter.PageSize, cancellationToken);
    }

    public void Add(TodoItem todoItem) => context.TodoItems.Add(todoItem);

    public void Remove(TodoItem todoItem) => context.TodoItems.Remove(todoItem);

    // The search term is literal text: '%' and '_' must not act as LIKE wildcards.
    private static string EscapeLikePattern(string value) =>
        value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal);

    private static Expression<Func<TodoItem, object>> GetSortProperty(string? sortColumn) =>
        sortColumn?.ToUpperInvariant() switch
        {
            "DESCRIPTION" => todoItem => todoItem.Description,
            "DUEDATE" => todoItem => todoItem.DueDate!,
            "PRIORITY" => todoItem => todoItem.Priority,
            _ => todoItem => todoItem.CreatedAt
        };
}
