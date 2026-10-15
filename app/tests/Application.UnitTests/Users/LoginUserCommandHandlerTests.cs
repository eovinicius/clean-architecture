using Application.Abstractions.Authentication;
using Application.UnitTests.Abstractions;
using Application.Users;
using Application.Users.Login;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class LoginUserCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "test@example.com";
    private const string Password = "Password123";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentialsAndStillHash_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            CreateTokenProvider(),
            CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
        passwordHasher.Received(1).Hash(Password);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidCredentials_WhenPasswordIsInvalid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var handler = new LoginUserCommandHandler(
            context,
            passwordHasher,
            CreateTokenProvider(),
            CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_Should_ReturnTokensAndPersistHashedRefreshToken_WhenCredentialsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        var handler = new LoginUserCommandHandler(
            context,
            CreateVerifyingPasswordHasher(),
            CreateTokenProvider(),
            CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("refresh-token");

        RefreshToken refreshToken = await context.RefreshTokens.SingleAsync();
        refreshToken.TokenHash.ShouldBe("hash:refresh-token");
        refreshToken.FamilyId.ShouldNotBe(Guid.Empty);
        refreshToken.RevokedAtUtc.ShouldBeNull();
        refreshToken.ExpiresOnUtc.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public async Task Handle_Should_FindUser_WhenEmailCasingDiffers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);

        var handler = new LoginUserCommandHandler(
            context,
            CreateVerifyingPasswordHasher(),
            CreateTokenProvider(),
            CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand("  Test@Example.COM ", Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_RemoveExpiredRefreshTokensOfUser_WhenCredentialsAreValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context);
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = "hash:expired",
            FamilyId = Guid.NewGuid(),
            UserId = UserId,
            ExpiresOnUtc = Now.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var handler = new LoginUserCommandHandler(
            context,
            CreateVerifyingPasswordHasher(),
            CreateTokenProvider(),
            CreateDateTimeProvider());

        // Act
        Result<AccessTokensResponse> result = await handler.Handle(
            new LoginUserCommand(Email, Password),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.RefreshTokens.AnyAsync(rt => rt.TokenHash == "hash:expired")).ShouldBeFalse();
        (await context.RefreshTokens.CountAsync()).ShouldBe(1);
    }

    private static IPasswordHasher CreateVerifyingPasswordHasher()
    {
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        return passwordHasher;
    }

    private static ITokenProvider CreateTokenProvider()
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.Create(Arg.Any<User>()).Returns("access-token");
        tokenProvider.GenerateRefreshToken().Returns("refresh-token");
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

    private static async Task SeedUserAsync(TestDbContext context)
    {
        context.Users.Add(new User
        {
            Id = UserId,
            Email = Email,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash"
        });

        await context.SaveChangesAsync();
    }
}
