using Application.Abstractions.Messaging;

namespace Application.Users.DeleteExpiredUnconfirmed;

// Returns how many users were removed.
public sealed record DeleteExpiredUnconfirmedUsersCommand : ICommand<int>;
