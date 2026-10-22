using Application.Abstractions.Authentication;
using Domain.Users;

namespace Application.Users;

internal static class RefreshTokenIssuer
{
    // Only the hash is persisted; the plain token goes back to the client exactly once.
    public static (RefreshToken Entity, string Token) Issue(
        ITokenProvider tokenProvider,
        Guid userId,
        Guid familyId,
        DateTime utcNow)
    {
        string token = tokenProvider.GenerateRefreshToken();

        var refreshToken = RefreshToken.Issue(
            userId,
            tokenProvider.HashRefreshToken(token),
            familyId,
            utcNow.Add(tokenProvider.RefreshTokenLifetime));

        return (refreshToken, token);
    }
}
