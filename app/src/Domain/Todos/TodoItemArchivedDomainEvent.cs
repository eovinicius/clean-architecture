using SharedKernel;

namespace Domain.Todos;

public sealed record TodoItemArchivedDomainEvent(Guid TodoItemId) : IDomainEvent;
