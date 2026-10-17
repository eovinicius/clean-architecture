using System.Linq.Expressions;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Todos.Get;

internal sealed class GetTodosQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetTodosQuery, PagedList<TodoResponse>>
{
    public async Task<Result<PagedList<TodoResponse>>> Handle(GetTodosQuery query, CancellationToken cancellationToken)
    {
        if (query.UserId != userContext.UserId)
        {
            return Result.Failure<PagedList<TodoResponse>>(UserErrors.Unauthorized());
        }

        IQueryable<TodoItem> todosQuery = context.TodoItems
            .Where(todoItem => todoItem.UserId == query.UserId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string pattern = $"%{EscapeLikePattern(query.Search)}%";

            todosQuery = todosQuery.Where(todoItem => EF.Functions.ILike(todoItem.Description, pattern, LikeEscapeCharacter));
        }

        todosQuery = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase)
            ? todosQuery.OrderByDescending(GetSortProperty(query.SortColumn))
            : todosQuery.OrderBy(GetSortProperty(query.SortColumn));

        IQueryable<TodoResponse> todoResponsesQuery = todosQuery
            .Select(todoItem => new TodoResponse
            {
                Id = todoItem.Id,
                UserId = todoItem.UserId,
                Description = todoItem.Description,
                DueDate = todoItem.DueDate,
                Labels = todoItem.Labels,
                IsCompleted = todoItem.IsCompleted,
                CreatedAt = todoItem.CreatedAt,
                CompletedAt = todoItem.CompletedAt
            });

        PagedList<TodoResponse> todos = await PagedList<TodoResponse>.CreateAsync(
            todoResponsesQuery,
            query.Page,
            query.PageSize,
            cancellationToken);

        return todos;
    }

    private const string LikeEscapeCharacter = @"\";

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
