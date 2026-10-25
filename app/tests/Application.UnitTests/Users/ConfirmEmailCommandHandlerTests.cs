using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.UnitTests.Abstractions;
using Application.Users.ConfirmEmail;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class ConfirmEmailCommandHandlerTests : BaseHandlerTest
{
    private const string Token = "token";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact] // SPEC-0003 AC-3
    public async Task Handle_Should_ConfirmEmailAndMarkTokenUsed_WhenTokenIsValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, UserToken token) = await SeedAsync(context);
        ConfirmEmailCommandHandler handler = CreateHandler(context, Now.AddHours(1));

        // Act
        Result result = await handler.Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailConfirmedAt.ShouldBe(Now.AddHours(1));
        (await context.UserTokens.SingleAsync(t => t.Id == token.Id)).UsedAtUtc.ShouldBe(Now.AddHours(1));
    }

    [Fact] // SPEC-0003 AC-4
    public async Task Handle_Should_ReturnInvalidToken_WhenTokenDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedAsync(context);
        ConfirmEmailCommandHandler handler = CreateHandler(context, Now);

        // Act
        Result result = await handler.Handle(new ConfirmEmailCommand("unknown"), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidEmailConfirmationToken);
    }

    [Fact] // SPEC-0003 AC-4
    public async Task Handle_Should_ReturnInvalidToken_WhenTokenWasAlreadyUsed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedAsync(context);
        await CreateHandler(context, Now).Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Act
        Result result = await CreateHandler(context, Now.AddMinutes(1))
            .Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidEmailConfirmationToken);
    }

    [Theory] // SPEC-0003 AC-4 (RN-3)
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Handle_Should_AcceptTokenUntilExactExpiration_AndRejectAfterIt(long ticksAfterExpiration, bool succeeds)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        ConfirmEmailCommandHandler handler = CreateHandler(context, Now.AddHours(24).AddTicks(ticksAfterExpiration));

        // Act
        Result result = await handler.Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBe(succeeds);
        if (!succeeds)
        {
            result.Error.ShouldBe(UserErrors.InvalidEmailConfirmationToken);
            (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailConfirmedAt.ShouldBeNull();
        }
    }

    [Fact] // SPEC-0003 AC-5
    public async Task Handle_Should_RemoveOtherConfirmationTokensOfUser_WhenConfirmed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        var otherToken = UserToken.Issue(user.Id, UserTokenPurpose.EmailConfirmation, "hash:other", Now);
        context.UserTokens.Add(otherToken);
        await context.SaveChangesAsync();
        ConfirmEmailCommandHandler handler = CreateHandler(context, Now.AddMinutes(5));

        // Act
        Result result = await handler.Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.AnyAsync(t => t.Id == otherToken.Id)).ShouldBeFalse();
    }

    [Fact] // SPEC-0003 AC-7
    public async Task Handle_Should_KeepOriginalConfirmationInstant_WhenEmailIsAlreadyConfirmed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User user, _) = await SeedAsync(context);
        user.ConfirmEmail(Now.AddMinutes(1));
        await context.SaveChangesAsync();
        ConfirmEmailCommandHandler handler = CreateHandler(context, Now.AddMinutes(10));

        // Act
        Result result = await handler.Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).EmailConfirmedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact] // SPEC-0003 AC-6
    public async Task Handle_Should_ReturnInvalidToken_WhenAnotherRequestUsedTheTokenFirst()
    {
        // Arrange
        DbContextOptions<ConflictingDbContext> options = new DbContextOptionsBuilder<ConflictingDbContext>()
            .UseInMemoryDatabase($"clean-architecture-{Guid.NewGuid()}")
            .Options;
        await using var context = new ConflictingDbContext(options);

        var user = User.Create("test@example.com", "Test", "User", "hash", Now);
        context.Users.Add(user);
        context.UserTokens.Add(UserToken.Issue(user.Id, UserTokenPurpose.EmailConfirmation, $"hash:{Token}", Now));
        await context.SaveChangesAsync();
        context.ThrowConcurrencyConflictOnSave = true;

        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);
        var handler = new ConfirmEmailCommandHandler(context, tokenProvider, dateTimeProvider);

        // Act
        Result result = await handler.Handle(new ConfirmEmailCommand(Token), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.InvalidEmailConfirmationToken);
    }

    private static ConfirmEmailCommandHandler CreateHandler(TestDbContext context, DateTime utcNow)
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(utcNow);

        return new ConfirmEmailCommandHandler(context, tokenProvider, dateTimeProvider);
    }

    private static async Task<(User User, UserToken Token)> SeedAsync(TestDbContext context)
    {
        var user = User.Create("test@example.com", "Test", "User", "hash", Now);
        var token = UserToken.Issue(user.Id, UserTokenPurpose.EmailConfirmation, $"hash:{Token}", Now);
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
