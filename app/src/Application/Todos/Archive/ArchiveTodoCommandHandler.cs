using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Archive;

internal sealed class ArchiveTodoCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<ArchiveTodoCommand>
{
    public async Task<Result> Handle(ArchiveTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await context.TodoItems
            .SingleOrDefaultAsync(t => t.Id == command.TodoItemId && t.UserId == userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        // SPEC-0001 RN-4: arquivar é idempotente e preserva o ArchivedAt original.
        if (todoItem.IsArchived)
        {
            return Result.Success();
        }

        if (!todoItem.IsCompleted)
        {
            return Result.Failure(TodoItemErrors.NotCompleted(command.TodoItemId));
        }

        todoItem.IsArchived = true;
        todoItem.ArchivedAt = dateTimeProvider.UtcNow;

        todoItem.Raise(new TodoItemArchivedDomainEvent(todoItem.Id));

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
