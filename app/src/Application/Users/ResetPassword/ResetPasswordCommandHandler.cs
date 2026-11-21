using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.ResetPassword;

internal sealed partial class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IUserTokenRepository userTokenRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    ILogger<ResetPasswordCommandHandler> logger) : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string tokenHash = tokenProvider.HashUserToken(command.Token);

        UserToken? resetToken = await userTokenRepository.GetByTokenHashAsync(
            tokenHash,
            UserTokenPurpose.PasswordReset,
            cancellationToken);

        if (resetToken is null || !resetToken.IsUsable(now))
        {
            return UserErrors.InvalidPasswordResetToken;
        }

        User? user = await userRepository.GetByIdAsync(resetToken.UserId, cancellationToken);

        if (user is null)
        {
            return UserErrors.InvalidPasswordResetToken;
        }

        resetToken.MarkUsed(now);

        IReadOnlyList<UserToken> userTokens = await userTokenRepository.GetByUserAsync(
            user.Id,
            UserTokenPurpose.PasswordReset,
            cancellationToken);

        userTokenRepository.RemoveRange(userTokens.Where(t => t.Id != resetToken.Id));

        user.ResetPassword(passwordHasher.Hash(command.NewPassword));

        IReadOnlyList<RefreshToken> activeSessions =
            await refreshTokenRepository.GetActiveByUserAsync(user.Id, cancellationToken);

        foreach (RefreshToken session in activeSessions)
        {
            session.Revoke(now);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request used this token first.
            return UserErrors.InvalidPasswordResetToken;
        }

        LogPasswordReset(logger, user.Id, activeSessions.Count);

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset for user {UserId}; {RevokedSessions} sessions revoked")]
    private static partial void LogPasswordReset(ILogger logger, Guid userId, int revokedSessions);
}
