using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.ConfirmEmail;

internal sealed class ConfirmEmailCommandHandler(
    IUserRepository userRepository,
    IUserTokenRepository userTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ConfirmEmailCommand>
{
    public async Task<Result> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string tokenHash = tokenProvider.HashUserToken(command.Token);

        UserToken? confirmationToken = await userTokenRepository.GetByTokenHashAsync(
            tokenHash,
            UserTokenPurpose.EmailConfirmation,
            cancellationToken);

        if (confirmationToken is null || !confirmationToken.IsUsable(now))
        {
            return UserErrors.InvalidEmailConfirmationToken;
        }

        User? user = await userRepository.GetByIdAsync(confirmationToken.UserId, cancellationToken);

        if (user is null)
        {
            return UserErrors.InvalidEmailConfirmationToken;
        }

        confirmationToken.MarkUsed(now);

        IReadOnlyList<UserToken> userTokens = await userTokenRepository.GetByUserAsync(
            user.Id,
            UserTokenPurpose.EmailConfirmation,
            cancellationToken);

        userTokenRepository.RemoveRange(userTokens.Where(t => t.Id != confirmationToken.Id));

        user.ConfirmEmail(now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request used this token first.
            return UserErrors.InvalidEmailConfirmationToken;
        }

        return Result.Success();
    }
}
