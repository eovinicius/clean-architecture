using Domain.Users;

namespace Application.Abstractions.Data;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    void Add(User user);

    // A single DELETE evaluated in the database, so a confirmation that lands meanwhile is not lost;
    // the user's tokens go with it through the cascading foreign key.
    Task<int> DeleteExpiredUnconfirmedAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
