using Application.Abstractions.Messaging;

namespace Application.Todos.Archive;

public sealed record ArchiveTodoCommand(Guid TodoItemId) : ICommand;
