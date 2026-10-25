using Application.Abstractions.Authentication;
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
}
