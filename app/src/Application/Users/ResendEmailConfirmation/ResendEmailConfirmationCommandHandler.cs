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

        string? token = await UserTokenIssuer.ReissueAsync(
            context,
            tokenProvider,
            user.Id,
            UserTokenPurpose.EmailConfirmation,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        await context.SaveChangesAsync(cancellationToken);

        await EmailConfirmationEmail.SendAsync(emailSender, user.Email, token);

        return Result.Success();
    }
}
