using Application.Abstractions.Messaging;

namespace Application.Users.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email) : ICommand;
