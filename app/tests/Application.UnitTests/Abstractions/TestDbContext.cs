using Application.Abstractions.Data;
using Application.Abstractions.Pagination;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// A lightweight in-memory <see cref="DbContext"/> that implements the Application's repositories and
/// <see cref="IUnitOfWork"/>, so handlers can be unit tested without referencing the Infrastructure layer.
/// Changes stay pending until <see cref="IUnitOfWork.SaveChangesAsync"/>, as with the real unit of work.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options),
        IUnitOfWork,
        ITodoItemRepository,
        IUserRepository,
        IRefreshTokenRepository,
        IUserTokenRepository
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<UserToken> UserTokens { get; set; }

    public DbSet<TodoItem> TodoItems { get; set; }

    // Simulates the xmin conflict raised by PostgreSQL when two requests change the same row.
    public bool ThrowConcurrencyConflictOnSave { get; set; }

    public IUnitOfWork UnitOfWork => this;

    public ITodoItemRepository TodoItemRepository => this;

    public IUserRepository UserRepository => this;

    public IRefreshTokenRepository RefreshTokenRepository => this;

    public IUserTokenRepository UserTokenRepository => this;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        ThrowConcurrencyConflictOnSave
            ? throw new ConcurrencyConflictException()
            : SaveChangesAsync(cancellationToken);

    Task<TodoItem?> ITodoItemRepository.GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        TodoItems.SingleOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

    async Task<PagedList<TodoItem>> ITodoItemRepository.GetPagedAsync(
        TodoItemListFilter filter,
        CancellationToken cancellationToken)
    {
        List<TodoItem> items = await TodoItems
            .Where(t => t.UserId == filter.UserId && (filter.IncludeArchived || !t.IsArchived))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return new PagedList<TodoItem>
        {
            Items = [.. items.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)],
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = items.Count
        };
    }

    void ITodoItemRepository.Add(TodoItem todoItem) => TodoItems.Add(todoItem);

    void ITodoItemRepository.Remove(TodoItem todoItem) => TodoItems.Remove(todoItem);

    Task<User?> IUserRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    Task<User?> IUserRepository.GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    Task<bool> IUserRepository.ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        Users.AnyAsync(u => u.Id == id, cancellationToken);

    Task<bool> IUserRepository.EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        Users.AnyAsync(u => u.Email == email, cancellationToken);

    void IUserRepository.Add(User user) => Users.Add(user);

    async Task<int> IUserRepository.DeleteExpiredUnconfirmedAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        List<User> expired = await Users.Where(User.IsUnconfirmedRegistrationExpired(utcNow)).ToListAsync(cancellationToken);

        Users.RemoveRange(expired);
        await SaveChangesAsync(cancellationToken);

        return expired.Count;
    }

    Task<RefreshToken?> IRefreshTokenRepository.GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    async Task<IReadOnlyList<RefreshToken>> IRefreshTokenRepository.GetExpiredByUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await RefreshTokens.Where(rt => rt.UserId == userId && rt.ExpiresOnUtc < utcNow).ToListAsync(cancellationToken);

    async Task<IReadOnlyList<RefreshToken>> IRefreshTokenRepository.GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null).ToListAsync(cancellationToken);

    async Task<IReadOnlyList<RefreshToken>> IRefreshTokenRepository.GetActiveByFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken) =>
        await RefreshTokens.Where(rt => rt.FamilyId == familyId && rt.RevokedAtUtc == null).ToListAsync(cancellationToken);

    void IRefreshTokenRepository.Add(RefreshToken refreshToken) => RefreshTokens.Add(refreshToken);

    void IRefreshTokenRepository.RemoveRange(IEnumerable<RefreshToken> refreshTokens) => RefreshTokens.RemoveRange(refreshTokens);

    Task<UserToken?> IUserTokenRepository.GetByTokenHashAsync(
        string tokenHash,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken) =>
        UserTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose, cancellationToken);

    async Task<IReadOnlyList<UserToken>> IUserTokenRepository.GetByUserAsync(
        Guid userId,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken) =>
        await UserTokens.Where(t => t.UserId == userId && t.Purpose == purpose).ToListAsync(cancellationToken);

    void IUserTokenRepository.Add(UserToken userToken) => UserTokens.Add(userToken);

    void IUserTokenRepository.RemoveRange(IEnumerable<UserToken> userTokens) => UserTokens.RemoveRange(userTokens);
}
