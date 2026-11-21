using Domain.Users;

namespace Application.Abstractions.Data;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetExpiredByUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);

    void RemoveRange(IEnumerable<RefreshToken> refreshTokens);
}
