using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Notifications;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUserTokenRepository userTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        if (await userRepository.EmailExistsAsync(email, cancellationToken))
        {
            return UserErrors.EmailNotUnique;
        }

        DateTime now = dateTimeProvider.UtcNow;

        var user = User.Create(email, command.FirstName, command.LastName, passwordHasher.Hash(command.Password), now);

        userRepository.Add(user);

        (UserToken confirmationToken, string token) =
            UserTokenIssuer.Issue(tokenProvider, user.Id, UserTokenPurpose.EmailConfirmation, now);

        userTokenRepository.Add(confirmationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await EmailConfirmationEmail.SendAsync(emailSender, user.Email, token);

        return user.Id;
    }
}
