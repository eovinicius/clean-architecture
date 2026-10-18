using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Unarchive;

internal sealed class UnarchiveTodoCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<UnarchiveTodoCommand>
{
    public async Task<Result> Handle(UnarchiveTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await context.TodoItems
            .SingleOrDefaultAsync(t => t.Id == command.TodoItemId && t.UserId == userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        // SPEC-0001 RN-4: desarquivar uma tarefa não arquivada não altera nada.
        if (!todoItem.IsArchived)
        {
            return Result.Success();
        }

        todoItem.IsArchived = false;
        todoItem.ArchivedAt = null;

        todoItem.Raise(new TodoItemUnarchivedDomainEvent(todoItem.Id));

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
