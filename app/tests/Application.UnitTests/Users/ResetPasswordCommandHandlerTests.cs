using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users.ResetPassword;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class ResetPasswordCommandHandlerTests : BaseHandlerTest
{
    private const string Token = "token";
    private const string NewPassword = "NewPassword123";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact] // SPEC-0004 AC-5
    public async Task Handle_Should_ReplacePasswordHashAndMarkTokenUsed_WhenTokenIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, UserToken token) = await SeedAsync(context);
        ResetPasswordCommandHandler handler = CreateHandler(context, Now.AddMinutes(10));

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).PasswordHash.ShouldBe($"hashed:{NewPassword}");
        (await context.UserTokens.SingleAsync(t => t.Id == token.Id)).UsedAtUtc.ShouldBe(Now.AddMinutes(10));
    }

    [Fact] // SPEC-0004 AC-6
    public async Task Handle_Should_ReturnInvalidTokenAndKeepPassword_WhenTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        ResetPasswordCommandHandler handler = CreateHandler(context, Now);

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand("unknown", NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidPasswordResetToken);
        (await context.Users.SingleAsync(u => u.Id == user.Id)).PasswordHash.ShouldBe("old-hash");
    }

    [Fact] // SPEC-0004 AC-6
    public async Task Handle_Should_ReturnInvalidToken_WhenTokenWasAlreadyUsed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedAsync(context);
        await CreateHandler(context, Now).Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Act
        Result result = await CreateHandler(context, Now.AddMinutes(1))
            .Handle(new ResetPasswordCommand(Token, "AnotherPassword123"), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidPasswordResetToken);
    }

    [Fact] // SPEC-0004 AC-6
    public async Task Handle_Should_ReturnInvalidToken_WhenGivenAConfirmationToken()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        context.UserTokens.Add(UserToken.Issue(user.Id, UserTokenPurpose.EmailConfirmation, "hash:confirmation", Now));
        await context.SaveChangesAsync();
        ResetPasswordCommandHandler handler = CreateHandler(context, Now);

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand("confirmation", NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidPasswordResetToken);
    }

    [Theory] // SPEC-0004 AC-6 (RN-3)
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Handle_Should_AcceptTokenUntilExactExpiration_AndRejectAfterIt(long ticksAfterExpiration, bool succeeds)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        ResetPasswordCommandHandler handler = CreateHandler(context, Now.AddHours(1).AddTicks(ticksAfterExpiration));

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBe(succeeds);
        if (!succeeds)
        {
            result.Error.ShouldBe(UserErrors.InvalidPasswordResetToken);
            (await context.Users.SingleAsync(u => u.Id == user.Id)).PasswordHash.ShouldBe("old-hash");
        }
    }

    [Fact] // SPEC-0004 AC-6 (RN-5)
    public async Task Handle_Should_RemoveOtherResetTokensOfUser_WhenPasswordIsReset()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        var otherToken = UserToken.Issue(user.Id, UserTokenPurpose.PasswordReset, "hash:other", Now);
        context.UserTokens.Add(otherToken);
        await context.SaveChangesAsync();
        ResetPasswordCommandHandler handler = CreateHandler(context, Now.AddMinutes(1));

        // Act
        await handler.Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        (await context.UserTokens.AnyAsync(t => t.Id == otherToken.Id)).ShouldBeFalse();
    }

    [Fact] // SPEC-0004 AC-10
    public async Task Handle_Should_RevokeEveryActiveRefreshToken_AndKeepAlreadyRevokedInstant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        var firstSession = RefreshToken.Issue(user.Id, "hash:first", Guid.NewGuid(), Now.AddDays(7));
        var secondSession = RefreshToken.Issue(user.Id, "hash:second", Guid.NewGuid(), Now.AddDays(7));
        var alreadyRevoked = RefreshToken.Issue(user.Id, "hash:revoked", Guid.NewGuid(), Now.AddDays(7));
        alreadyRevoked.Revoke(Now.AddDays(-1));
        context.RefreshTokens.AddRange(firstSession, secondSession, alreadyRevoked);
        await context.SaveChangesAsync();
        ResetPasswordCommandHandler handler = CreateHandler(context, Now.AddMinutes(1));

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.RefreshTokens.SingleAsync(t => t.Id == firstSession.Id)).RevokedAtUtc.ShouldBe(Now.AddMinutes(1));
        (await context.RefreshTokens.SingleAsync(t => t.Id == secondSession.Id)).RevokedAtUtc.ShouldBe(Now.AddMinutes(1));
        (await context.RefreshTokens.SingleAsync(t => t.Id == alreadyRevoked.Id)).RevokedAtUtc.ShouldBe(Now.AddDays(-1));
    }

    [Fact] // SPEC-0004 AC-11 (RN-13)
    public async Task Handle_Should_NotConfirmEmail_WhenEmailIsUnconfirmed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context, confirmEmail: false);
        ResetPasswordCommandHandler handler = CreateHandler(context, Now);

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailConfirmedAt.ShouldBeNull();
    }

    [Fact] // SPEC-0004 AC-7
    public async Task Handle_Should_ReturnInvalidToken_WhenAnotherRequestUsedTheTokenFirst()
    {
        // Arrange
        DbContextOptions<ConflictingDbContext> options = new DbContextOptionsBuilder<ConflictingDbContext>()
            .UseInMemoryDatabase($"clean-architecture-{Guid.NewGuid()}")
            .Options;
        await using var context = new ConflictingDbContext(options);

        var user = User.Create("test@example.com", "Test", "User", "old-hash", Now);
        context.Users.Add(user);
        context.UserTokens.Add(UserToken.Issue(user.Id, UserTokenPurpose.PasswordReset, $"hash:{Token}", Now));
        await context.SaveChangesAsync();
        context.ThrowConcurrencyConflictOnSave = true;

        var handler = new ResetPasswordCommandHandler(
            context,
            CreateTokenProvider(),
            CreatePasswordHasher(),
            CreateDateTimeProvider(Now),
            NullLogger<ResetPasswordCommandHandler>.Instance);

        // Act
        Result result = await handler.Handle(new ResetPasswordCommand(Token, NewPassword), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidPasswordResetToken);
    }

    private static ResetPasswordCommandHandler CreateHandler(TestDbContext context, DateTime utcNow) =>
        new(
            context,
            CreateTokenProvider(),
            CreatePasswordHasher(),
            CreateDateTimeProvider(utcNow),
            NullLogger<ResetPasswordCommandHandler>.Instance);

    private static ITokenProvider CreateTokenProvider()
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");

        return tokenProvider;
    }

    private static IPasswordHasher CreatePasswordHasher()
    {
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Arg.Any<string>()).Returns(call => $"hashed:{call.Arg<string>()}");

        return passwordHasher;
    }

    private static IDateTimeProvider CreateDateTimeProvider(DateTime utcNow)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(utcNow);

        return dateTimeProvider;
    }

    private static async Task<(User User, UserToken Token)> SeedAsync(TestDbContext context, bool confirmEmail = true)
    {
        var user = User.Create("test@example.com", "Test", "User", "old-hash", Now);
        if (confirmEmail)
        {
            user.ConfirmEmail(Now);
        }

        var token = UserToken.Issue(user.Id, UserTokenPurpose.PasswordReset, $"hash:{Token}", Now);
        context.Users.Add(user);
        context.UserTokens.Add(token);

        await context.SaveChangesAsync();
        user.ClearDomainEvents();

        return (user, token);
    }

    // Simulates the xmin conflict raised by PostgreSQL when two requests use the same token.
    private sealed class ConflictingDbContext(DbContextOptions<ConflictingDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public bool ThrowConcurrencyConflictOnSave { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<UserToken> UserTokens { get; set; }

        public DbSet<TodoItem> TodoItems { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            ThrowConcurrencyConflictOnSave
                ? throw new DbUpdateConcurrencyException()
                : base.SaveChangesAsync(cancellationToken);
    }
}
