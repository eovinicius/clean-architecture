using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Domain.Users;
using SharedKernel;

namespace Application.Todos.Copy;

internal sealed class CopyTodoCommandHandler(
    IUserRepository userRepository,
    ITodoItemRepository todoItemRepository,
    IUnitOfWork unitOfWork,
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

        if (!await userRepository.ExistsAsync(command.UserId, cancellationToken))
        {
            return UserErrors.NotFound(command.UserId);
        }

        TodoItem? existingTodo = await todoItemRepository.GetByIdAsync(command.TodoId, command.UserId, cancellationToken);

        if (existingTodo is null)
        {
            return TodoItemErrors.NotFound(command.TodoId);
        }

        TodoItem copiedTodoItem = existingTodo.Copy(dateTimeProvider.UtcNow);

        todoItemRepository.Add(copiedTodoItem);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return copiedTodoItem.Id;
    }
}
