using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Todos.Copy;

internal sealed class CopyTodoCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext)
    : ICommandHandler<CopyTodoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CopyTodoCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId != command.UserId)
        {
            return UserErrors.Unauthorized();
        }

        bool userExists = await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken);

        if (!userExists)
        {
            return UserErrors.NotFound(command.UserId);
        }

        TodoItem? existingTodo = await context.TodoItems.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == command.TodoId && t.UserId == command.UserId, cancellationToken);

        if (existingTodo is null)
        {
            return TodoItemErrors.NotFound(command.TodoId);
        }

        TodoItem copiedTodoItem = existingTodo.Copy(dateTimeProvider.UtcNow);

        context.TodoItems.Add(copiedTodoItem);

        await context.SaveChangesAsync(cancellationToken);

        return copiedTodoItem.Id;
    }
}
