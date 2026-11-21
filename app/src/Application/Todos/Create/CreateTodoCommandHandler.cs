using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Domain.Users;
using SharedKernel;

namespace Application.Todos.Create;

internal sealed class CreateTodoCommandHandler(
    IUserRepository userRepository,
    ITodoItemRepository todoItemRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext)
    : ICommandHandler<CreateTodoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTodoCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId != command.UserId)
        {
            return UserErrors.Unauthorized();
        }

        if (!await userRepository.ExistsAsync(command.UserId, cancellationToken))
        {
            return UserErrors.NotFound(command.UserId);
        }

        var todoItem = TodoItem.Create(
            command.UserId,
            command.Description,
            command.Priority,
            command.DueDate,
            command.Labels,
            dateTimeProvider.UtcNow);

        todoItemRepository.Add(todoItem);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return todoItem.Id;
    }
}
