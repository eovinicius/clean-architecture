using Application.Abstractions.Messaging;

namespace Application.Users.ResendEmailConfirmation;

public sealed record ResendEmailConfirmationCommand(string Email) : ICommand;
