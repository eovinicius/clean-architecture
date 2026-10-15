using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string tokenHash = tokenProvider.HashRefreshToken(command.RefreshToken);

        RefreshToken? refreshToken = await context.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null || refreshToken.ExpiresOnUtc < now)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (refreshToken.RevokedAtUtc is not null)
        {
            // A revoked token was presented again: assume it was stolen and revoke the whole family.
            await RevokeFamilyAsync(refreshToken.FamilyId, now, cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == refreshToken.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        string accessToken = tokenProvider.Create(user);
        string newRefreshToken = tokenProvider.GenerateRefreshToken();

        refreshToken.RevokedAtUtc = now;

        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenProvider.HashRefreshToken(newRefreshToken),
            FamilyId = refreshToken.FamilyId,
            UserId = refreshToken.UserId,
            ExpiresOnUtc = now.Add(tokenProvider.RefreshTokenLifetime)
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated this token first.
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        return new AccessTokensResponse(accessToken, newRefreshToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        List<RefreshToken> activeTokens = await context.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (RefreshToken token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
