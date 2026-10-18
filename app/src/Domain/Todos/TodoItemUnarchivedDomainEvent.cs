using SharedKernel;

namespace Domain.Todos;

public sealed record TodoItemUnarchivedDomainEvent(Guid TodoItemId) : IDomainEvent;
