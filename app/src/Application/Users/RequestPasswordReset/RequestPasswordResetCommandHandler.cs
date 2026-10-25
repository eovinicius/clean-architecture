using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Notifications;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider,
    IEmailSender emailSender) : ICommandHandler<RequestPasswordResetCommand>
{
    // SPEC-0004 RN-7: todo caminho responde sucesso, para não revelar se o e-mail está cadastrado.
    public async Task<Result> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            return Result.Success();
        }

        string? token = await UserTokenIssuer.ReissueAsync(
            context,
            tokenProvider,
            user.Id,
            UserTokenPurpose.PasswordReset,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        await context.SaveChangesAsync(cancellationToken);

        await PasswordResetEmail.SendAsync(emailSender, user.Email, token);

        return Result.Success();
    }
}
