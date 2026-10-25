using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.ConfirmEmail;

internal sealed class ConfirmEmailCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ConfirmEmailCommand>
{
    public async Task<Result> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string tokenHash = tokenProvider.HashUserToken(command.Token);

        UserToken? confirmationToken = await context.UserTokens
            .SingleOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.Purpose == UserTokenPurpose.EmailConfirmation,
                cancellationToken);

        if (confirmationToken is null || !confirmationToken.IsUsable(now))
        {
            return UserErrors.InvalidEmailConfirmationToken;
        }

        User? user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == confirmationToken.UserId, cancellationToken);

        if (user is null)
        {
            return UserErrors.InvalidEmailConfirmationToken;
        }

        confirmationToken.MarkUsed(now);

        // SPEC-0003 RN-5: usar um token invalida os outros de confirmação do mesmo Usuário.
        List<UserToken> otherTokens = await context.UserTokens
            .Where(t => t.UserId == user.Id &&
                        t.Purpose == UserTokenPurpose.EmailConfirmation &&
                        t.Id != confirmationToken.Id)
            .ToListAsync(cancellationToken);

        context.UserTokens.RemoveRange(otherTokens);

        user.ConfirmEmail(now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request used this token first.
            return UserErrors.InvalidEmailConfirmationToken;
        }

        return Result.Success();
    }
}
