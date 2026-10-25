using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

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

    // SPEC-0003 RN-5/RN-6 e SPEC-0004 RN-5/RN-6: os tokens válidos anteriores continuam valendo, e um novo só sai
    // depois do intervalo mínimo. Devolve null (sem emitir) dentro do intervalo; o chamador salva e envia o e-mail.
    public static async Task<string?> ReissueAsync(
        IApplicationDbContext context,
        ITokenProvider tokenProvider,
        Guid userId,
        UserTokenPurpose purpose,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        List<UserToken> tokens = await context.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose)
            .ToListAsync(cancellationToken);

        if (tokens.Exists(t => !t.AllowsReissue(utcNow)))
        {
            return null;
        }

        context.UserTokens.RemoveRange(tokens.Where(t => !t.IsUsable(utcNow)));

        (UserToken userToken, string token) = Issue(tokenProvider, userId, purpose, utcNow);

        context.UserTokens.Add(userToken);

        return token;
    }
}
