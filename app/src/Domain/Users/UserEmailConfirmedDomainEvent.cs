using SharedKernel;

namespace Domain.Users;

public sealed record UserEmailConfirmedDomainEvent(Guid UserId) : IDomainEvent;
