using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Spend the same CPU time as a real verification so response timing does not reveal registered emails.
            _ = passwordHasher.Hash(command.Password);

            return UserErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return UserErrors.InvalidCredentials;
        }

        // SPEC-0003 RN-12: só depois da senha, para o erro não revelar o estado da conta a quem não a conhece.
        if (!user.IsEmailConfirmed)
        {
            return UserErrors.EmailNotConfirmed;
        }

        DateTime now = dateTimeProvider.UtcNow;

        List<RefreshToken> expiredTokens = await context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.ExpiresOnUtc < now)
            .ToListAsync(cancellationToken);

        context.RefreshTokens.RemoveRange(expiredTokens);

        string accessToken = tokenProvider.Create(user);

        (RefreshToken refreshToken, string token) =
            RefreshTokenIssuer.Issue(tokenProvider, user.Id, familyId: Guid.NewGuid(), now);

        context.RefreshTokens.Add(refreshToken);

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, token);
    }
}
