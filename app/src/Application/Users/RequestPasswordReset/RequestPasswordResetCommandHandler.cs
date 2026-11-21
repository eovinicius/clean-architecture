using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Notifications;
using Domain.Users;
using SharedKernel;

namespace Application.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    IUserRepository userRepository,
    IUserTokenRepository userTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender) : ICommandHandler<RequestPasswordResetCommand>
{
    public async Task<Result> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        User? user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return Result.Success();
        }

        string? token = await UserTokenIssuer.ReissueAsync(
            userTokenRepository,
            tokenProvider,
            user.Id,
            UserTokenPurpose.PasswordReset,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await PasswordResetEmail.SendAsync(emailSender, user.Email, token);

        return Result.Success();
    }
}
