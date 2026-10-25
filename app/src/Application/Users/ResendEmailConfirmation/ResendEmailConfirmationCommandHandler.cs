using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Notifications;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.ResendEmailConfirmation;

internal sealed class ResendEmailConfirmationCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender) : ICommandHandler<ResendEmailConfirmationCommand>
{
    // SPEC-0003 RN-13: todo caminho responde sucesso, para não revelar se o e-mail existe ou já foi confirmado.
    public async Task<Result> Handle(ResendEmailConfirmationCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.IsEmailConfirmed)
        {
            return Result.Success();
        }

        DateTime now = dateTimeProvider.UtcNow;

        List<UserToken> tokens = await context.UserTokens
            .Where(t => t.UserId == user.Id && t.Purpose == UserTokenPurpose.EmailConfirmation)
            .ToListAsync(cancellationToken);

        if (tokens.Exists(t => !t.AllowsReissue(now)))
        {
            return Result.Success();
        }

        context.UserTokens.RemoveRange(tokens.Where(t => !t.IsUsable(now)));

        (UserToken confirmationToken, string token) =
            UserTokenIssuer.Issue(tokenProvider, user.Id, UserTokenPurpose.EmailConfirmation, now);

        context.UserTokens.Add(confirmationToken);

        await context.SaveChangesAsync(cancellationToken);

        await EmailConfirmationEmail.SendAsync(emailSender, user.Email, token);

        return Result.Success();
    }
}
