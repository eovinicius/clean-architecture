using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.GetById;

internal sealed class GetTodoByIdQueryHandler(
    ITodoItemRepository todoItemRepository,
    IUserContext userContext,
    HybridCache cache)
    : IQueryHandler<GetTodoByIdQuery, TodoResponse>
{
    public async Task<Result<TodoResponse>> Handle(GetTodoByIdQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        TodoResponse? todo = await cache.GetOrCreateAsync(
            TodoCacheKeys.ById(userId, query.TodoItemId),
            async cancellation =>
                await todoItemRepository.GetByIdAsync(query.TodoItemId, userId, cancellation) is { } todoItem
                    ? TodoResponse.From(todoItem)
                    : null,
            cancellationToken: cancellationToken);

        if (todo is null)
        {
            return TodoItemErrors.NotFound(query.TodoItemId);
        }

        return todo;
    }
}
