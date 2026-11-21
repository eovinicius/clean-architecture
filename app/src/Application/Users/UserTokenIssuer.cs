using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Users;

namespace Application.Users;

internal static class UserTokenIssuer
{
    // Only the hash is persisted; the plain token goes out exactly once, in the email.
    public static (UserToken Entity, string Token) Issue(
        ITokenProvider tokenProvider,
        Guid userId,
        UserTokenPurpose purpose,
        DateTime utcNow)
    {
        string token = tokenProvider.GenerateUserToken();

        var userToken = UserToken.Issue(userId, purpose, tokenProvider.HashUserToken(token), utcNow);

        return (userToken, token);
    }

    public static async Task<string?> ReissueAsync(
        IUserTokenRepository userTokenRepository,
        ITokenProvider tokenProvider,
        Guid userId,
        UserTokenPurpose purpose,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UserToken> tokens = await userTokenRepository.GetByUserAsync(userId, purpose, cancellationToken);

        if (tokens.Any(t => !t.AllowsReissue(utcNow)))
        {
            return null;
        }

        userTokenRepository.RemoveRange(tokens.Where(t => !t.IsUsable(utcNow)));

        (UserToken userToken, string token) = Issue(tokenProvider, userId, purpose, utcNow);

        userTokenRepository.Add(userToken);

        return token;
    }
}
