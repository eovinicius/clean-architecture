using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Todos;
using Domain.Users;
using SharedKernel;

namespace Application.Todos.Get;

internal sealed class GetTodosQueryHandler(ITodoItemRepository todoItemRepository, IUserContext userContext)
    : IQueryHandler<GetTodosQuery, PagedList<TodoResponse>>
{
    public async Task<Result<PagedList<TodoResponse>>> Handle(GetTodosQuery query, CancellationToken cancellationToken)
    {
        if (query.UserId != userContext.UserId)
        {
            return UserErrors.Unauthorized();
        }

        var filter = new TodoItemListFilter(
            query.UserId,
            query.IncludeArchived,
            query.Search,
            query.SortColumn,
            query.SortOrder,
            query.Page,
            query.PageSize);

        PagedList<TodoItem> todoItems = await todoItemRepository.GetPagedAsync(filter, cancellationToken);

        return todoItems.Map(TodoResponse.From);
    }
}
