using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users.Register;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RegisterUserCommandHandlerTests : BaseHandlerTest
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

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
        (await context.UserTokens.AnyAsync()).ShouldBeFalse();
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

    private static RegisterUserCommandHandler CreateHandler(TestDbContext context, IPasswordHasher passwordHasher)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new RegisterUserCommandHandler(context.UserRepository, context.UnitOfWork, passwordHasher, dateTimeProvider);
    }
}
