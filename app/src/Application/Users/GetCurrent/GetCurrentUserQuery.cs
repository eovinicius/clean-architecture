using Application.Abstractions.Messaging;

namespace Application.Users.GetCurrent;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;
