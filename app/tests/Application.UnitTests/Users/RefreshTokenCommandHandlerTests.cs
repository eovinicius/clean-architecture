using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users;
using Application.Users.Refresh;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RefreshTokenCommandHandlerTests : BaseHandlerTest
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new RefreshTokenCommandHandler(context, CreateTokenProvider(), CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("missing-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenRefreshTokenIsExpired()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedAsync(context, "expired-token", expiresOnUtc: Now.AddDays(-1));

        var handler = new RefreshTokenCommandHandler(context, CreateTokenProvider(), CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("expired-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_Should_RevokeOldTokenAndIssueNewOneInSameFamily_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedAsync(context, "old-token", expiresOnUtc: Now.AddDays(1));

        var handler = new RefreshTokenCommandHandler(context, CreateTokenProvider(), CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("old-token"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("new-access-token");
        result.Value.RefreshToken.ShouldBe("new-refresh-token");

        RefreshToken oldToken = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == "hash:old-token");
        oldToken.RevokedAtUtc.ShouldBe(Now);

        RefreshToken newToken = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == "hash:new-refresh-token");
        newToken.FamilyId.ShouldBe(FamilyId);
        newToken.RevokedAtUtc.ShouldBeNull();
        newToken.ExpiresOnUtc.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public async Task Handle_Should_RevokeWholeFamily_WhenRevokedTokenIsReused()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedAsync(context, "reused-token", expiresOnUtc: Now.AddDays(1), revokedAtUtc: Now.AddHours(-1));
        context.RefreshTokens.Add(RefreshToken.Issue(userId, "hash:active-token", FamilyId, Now.AddDays(1)));
        await context.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(context, CreateTokenProvider(), CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("reused-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);

        RefreshToken activeToken = await context.RefreshTokens.SingleAsync(rt => rt.TokenHash == "hash:active-token");
        activeToken.RevokedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenAnotherRequestRotatedTheTokenFirst()
    {
        // Arrange
        DbContextOptions<ConflictingDbContext> options = new DbContextOptionsBuilder<ConflictingDbContext>()
            .UseInMemoryDatabase($"clean-architecture-{Guid.NewGuid()}")
            .Options;
        await using var context = new ConflictingDbContext(options);
        await SeedAsync(context, "contested-token", expiresOnUtc: Now.AddDays(1));
        context.ThrowConcurrencyConflictOnSave = true;

        var handler = new RefreshTokenCommandHandler(context, CreateTokenProvider(), CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new RefreshTokenCommand("contested-token"),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    private static ITokenProvider CreateTokenProvider()
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("new-access-token");
        tokenProvider.GenerateRefreshToken().Returns("new-refresh-token");
        tokenProvider.HashRefreshToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        tokenProvider.RefreshTokenLifetime.Returns(TimeSpan.FromDays(7));

        return tokenProvider;
    }

    private static IDateTimeProvider CreateDateTimeProvider()
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return dateTimeProvider;
    }

    private static async Task<Guid> SeedAsync(
        IApplicationDbContext context,
        string token,
        DateTime expiresOnUtc,
        DateTime? revokedAtUtc = null)
    {
        var user = User.Create("test@example.com", "Test", "User", "hash");
        context.Users.Add(user);

        var refreshToken = RefreshToken.Issue(user.Id, $"hash:{token}", FamilyId, expiresOnUtc);

        if (revokedAtUtc is not null)
        {
            refreshToken.Revoke(revokedAtUtc.Value);
        }

        context.RefreshTokens.Add(refreshToken);

        await context.SaveChangesAsync();

        return user.Id;
    }

    // Simulates the xmin conflict raised by PostgreSQL when two requests rotate the same token.
    private sealed class ConflictingDbContext(DbContextOptions<ConflictingDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public bool ThrowConcurrencyConflictOnSave { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<TodoItem> TodoItems { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ThrowConcurrencyConflictOnSave
                ? throw new DbUpdateConcurrencyException()
                : base.SaveChangesAsync(cancellationToken);
    }
}
