using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Complete;

internal sealed class CompleteTodoCommandHandler(
    ITodoItemRepository todoItemRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<CompleteTodoCommand>
{
    public async Task<Result> Handle(CompleteTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await todoItemRepository.GetByIdAsync(command.TodoItemId, userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return TodoItemErrors.NotFound(command.TodoItemId);
        }

        Result result = todoItem.Complete(dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
