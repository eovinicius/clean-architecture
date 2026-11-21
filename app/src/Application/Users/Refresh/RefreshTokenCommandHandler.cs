using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        string tokenHash = tokenProvider.HashRefreshToken(command.RefreshToken);

        RefreshToken? refreshToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (refreshToken is null || refreshToken.IsExpired(now))
        {
            return UserErrors.InvalidRefreshToken;
        }

        if (refreshToken.IsRevoked)
        {
            // A revoked token was presented again: assume it was stolen and revoke the whole family.
            await RevokeFamilyAsync(refreshToken.FamilyId, now, cancellationToken);

            return UserErrors.InvalidRefreshToken;
        }

        User? user = await userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);

        if (user is null)
        {
            return UserErrors.InvalidRefreshToken;
        }

        string accessToken = tokenProvider.Create(user);

        refreshToken.Revoke(now);

        (RefreshToken newRefreshToken, string token) =
            RefreshTokenIssuer.Issue(tokenProvider, refreshToken.UserId, refreshToken.FamilyId, now);

        refreshTokenRepository.Add(newRefreshToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request rotated this token first.
            return UserErrors.InvalidRefreshToken;
        }

        return new AccessTokensResponse(accessToken, token);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        IReadOnlyList<RefreshToken> activeTokens =
            await refreshTokenRepository.GetActiveByFamilyAsync(familyId, cancellationToken);

        foreach (RefreshToken token in activeTokens)
        {
            token.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
