using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.ResetPassword;

internal sealed partial class ResetPasswordCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    ILogger<ResetPasswordCommandHandler> logger) : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string tokenHash = tokenProvider.HashUserToken(command.Token);

        UserToken? resetToken = await context.UserTokens
            .SingleOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.Purpose == UserTokenPurpose.PasswordReset,
                cancellationToken);

        if (resetToken is null || !resetToken.IsUsable(now))
        {
            return UserErrors.InvalidPasswordResetToken;
        }

        User? user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == resetToken.UserId, cancellationToken);

        if (user is null)
        {
            return UserErrors.InvalidPasswordResetToken;
        }

        resetToken.MarkUsed(now);

        // SPEC-0004 RN-5: usar um token invalida os outros de redefinição do mesmo Usuário.
        List<UserToken> otherTokens = await context.UserTokens
            .Where(t => t.UserId == user.Id &&
                        t.Purpose == UserTokenPurpose.PasswordReset &&
                        t.Id != resetToken.Id)
            .ToListAsync(cancellationToken);

        context.UserTokens.RemoveRange(otherTokens);

        user.ResetPassword(passwordHasher.Hash(command.NewPassword));

        // SPEC-0004 RN-11: encerra todas as sessões; os JWT já emitidos valem até expirar.
        List<RefreshToken> activeSessions = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken session in activeSessions)
        {
            session.Revoke(now);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
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
