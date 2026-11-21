using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

internal sealed class RefreshTokenRepository(ApplicationDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetExpiredByUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.ExpiresOnUtc < utcNow)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);

    public void RemoveRange(IEnumerable<RefreshToken> refreshTokens) => context.RefreshTokens.RemoveRange(refreshTokens);
}
