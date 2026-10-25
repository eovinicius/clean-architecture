using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Notifications;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        if (await context.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return UserErrors.EmailNotUnique;
        }

        DateTime now = dateTimeProvider.UtcNow;

        var user = User.Create(email, command.FirstName, command.LastName, passwordHasher.Hash(command.Password), now);

        context.Users.Add(user);

        (UserToken confirmationToken, string token) =
            UserTokenIssuer.Issue(tokenProvider, user.Id, UserTokenPurpose.EmailConfirmation, now);

        context.UserTokens.Add(confirmationToken);

        await context.SaveChangesAsync(cancellationToken);

        await EmailConfirmationEmail.SendAsync(emailSender, user.Email, token);

        return user.Id;
    }
}
