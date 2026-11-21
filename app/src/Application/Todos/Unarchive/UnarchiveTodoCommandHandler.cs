using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Unarchive;

internal sealed class UnarchiveTodoCommandHandler(
    ITodoItemRepository todoItemRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<UnarchiveTodoCommand>
{
    public async Task<Result> Handle(UnarchiveTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await todoItemRepository.GetByIdAsync(command.TodoItemId, userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return TodoItemErrors.NotFound(command.TodoItemId);
        }

        todoItem.Unarchive();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
