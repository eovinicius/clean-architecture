using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Notifications;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Register;

// Runs after the user is committed (eventual consistency): if it fails, the registration stands and
// the user can still get a token through ResendEmailConfirmation.
internal sealed class UserRegisteredDomainEventHandler(
    IUserRepository userRepository,
    IUserTokenRepository userTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public async Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdAsync(domainEvent.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        (UserToken confirmationToken, string token) =
            UserTokenIssuer.Issue(tokenProvider, user.Id, UserTokenPurpose.EmailConfirmation, dateTimeProvider.UtcNow);

        userTokenRepository.Add(confirmationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await EmailConfirmationEmail.SendAsync(emailSender, user.Email, token);
    }
}
