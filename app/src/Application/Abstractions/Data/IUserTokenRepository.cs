using Domain.Users;

namespace Application.Abstractions.Data;

public interface IUserTokenRepository
{
    Task<UserToken?> GetByTokenHashAsync(string tokenHash, UserTokenPurpose purpose, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserToken>> GetByUserAsync(Guid userId, UserTokenPurpose purpose, CancellationToken cancellationToken = default);

    void Add(UserToken userToken);

    void RemoveRange(IEnumerable<UserToken> userTokens);
}
