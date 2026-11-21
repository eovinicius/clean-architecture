using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

internal sealed class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public void Add(User user) => context.Users.Add(user);

    public Task<int> DeleteExpiredUnconfirmedAsync(DateTime utcNow, CancellationToken cancellationToken = default) =>
        context.Users
            .Where(User.IsUnconfirmedRegistrationExpired(utcNow))
            .ExecuteDeleteAsync(cancellationToken);
}
