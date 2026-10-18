using Application.Abstractions.Messaging;

namespace Application.Todos.Unarchive;

public sealed record UnarchiveTodoCommand(Guid TodoItemId) : ICommand;
