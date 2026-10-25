using Application.Abstractions.Authentication;
using Application.Abstractions.Notifications;
using Application.UnitTests.Abstractions;
using Application.Users.Register;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private const string ConfirmationToken = "confirmation-token";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    private static RegisterUserCommand Command =>
        new("test@example.com", "Test", "User", "Password123");

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailIsNotUnique()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(User.Create(Command.Email, "Existing", "User", "hash", DateTime.UnixEpoch));
        await context.SaveChangesAsync();

        RegisterUserCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.EmailNotUnique);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Handle_Should_CreateUserWithHashedPasswordAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Command.Password).Returns("hashed-password");

        RegisterUserCommandHandler handler = CreateHandler(context, passwordHasher);

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.Email.ShouldBe(Command.Email);
        user.PasswordHash.ShouldBe("hashed-password");
        user.RegisteredAt.ShouldBe(Now);
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserRegisteredDomainEvent);
    }

    [Fact] // SPEC-0003 AC-1, AC-2
    public async Task Handle_Should_PersistOnlyTokenHashAndSendTokenByEmail_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        RegisterUserCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        UserToken token = await context.UserTokens.SingleAsync();
        token.UserId.ShouldBe(result.Value);
        token.Purpose.ShouldBe(UserTokenPurpose.EmailConfirmation);
        token.TokenHash.ShouldBe($"hash:{ConfirmationToken}");
        token.CreatedAtUtc.ShouldBe(Now);

        await _emailSender.Received(1).SendAsync(
            Command.Email,
            Arg.Is<string>(subject => !subject.Contains(ConfirmationToken, StringComparison.Ordinal)),
            Arg.Is<string>(body => body.Contains(ConfirmationToken, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_StoreNormalizedEmail_WhenEmailHasMixedCaseAndSpaces()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        RegisterUserCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(
            Command with { Email = "  Test@Example.COM " },
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        User user = await context.Users.SingleAsync(u => u.Id == result.Value);
        user.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenEmailDiffersOnlyByCase()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        context.Users.Add(User.Create("test@example.com", "Existing", "User", "hash", DateTime.UnixEpoch));
        await context.SaveChangesAsync();

        RegisterUserCommandHandler handler = CreateHandler(context, Substitute.For<IPasswordHasher>());

        // Act
        Result<Guid> result = await handler.Handle(
            Command with { Email = "TEST@example.com" },
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.EmailNotUnique);
    }

    private RegisterUserCommandHandler CreateHandler(TestDbContext context, IPasswordHasher passwordHasher)
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.GenerateUserToken().Returns(ConfirmationToken);
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new RegisterUserCommandHandler(context, passwordHasher, tokenProvider, dateTimeProvider, _emailSender);
    }
}
