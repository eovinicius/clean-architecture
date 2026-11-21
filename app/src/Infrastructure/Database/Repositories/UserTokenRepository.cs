using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

internal sealed class UserTokenRepository(ApplicationDbContext context) : IUserTokenRepository
{
    public Task<UserToken?> GetByTokenHashAsync(
        string tokenHash,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken = default) =>
        context.UserTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose, cancellationToken);

    public async Task<IReadOnlyList<UserToken>> GetByUserAsync(
        Guid userId,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken = default) =>
        await context.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose)
            .ToListAsync(cancellationToken);

    public void Add(UserToken userToken) => context.UserTokens.Add(userToken);

    public void RemoveRange(IEnumerable<UserToken> userTokens) => context.UserTokens.RemoveRange(userTokens);
}
