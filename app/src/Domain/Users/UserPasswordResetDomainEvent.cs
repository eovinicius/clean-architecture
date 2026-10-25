using SharedKernel;

namespace Domain.Users;

public sealed record UserPasswordResetDomainEvent(Guid UserId) : IDomainEvent;
