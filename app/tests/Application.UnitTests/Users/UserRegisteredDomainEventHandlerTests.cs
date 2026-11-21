using Application.Abstractions.Authentication;
using Application.Abstractions.Notifications;
using Application.UnitTests.Abstractions;
using Application.Users.Register;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class UserRegisteredDomainEventHandlerTests : BaseHandlerTest
{
    private const string ConfirmationToken = "confirmation-token";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    [Fact]
    public async Task Handle_Should_PersistOnlyTokenHashAndSendTokenByEmail_WhenUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("test@example.com", "Test", "User", "hash", Now);
        user.ClearDomainEvents();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        UserRegisteredDomainEventHandler handler = CreateHandler(context);

        // Act
        await handler.Handle(new UserRegisteredDomainEvent(user.Id), CancellationToken.None);

        // Assert
        UserToken token = await context.UserTokens.SingleAsync();
        token.UserId.ShouldBe(user.Id);
        token.Purpose.ShouldBe(UserTokenPurpose.EmailConfirmation);
        token.TokenHash.ShouldBe($"hash:{ConfirmationToken}");
        token.CreatedAtUtc.ShouldBe(Now);

        await _emailSender.Received(1).SendAsync(
            user.Email,
            Arg.Is<string>(subject => !subject.Contains(ConfirmationToken, StringComparison.Ordinal)),
            Arg.Is<string>(body => body.Contains(ConfirmationToken, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        UserRegisteredDomainEventHandler handler = CreateHandler(context);

        // Act
        await handler.Handle(new UserRegisteredDomainEvent(Guid.NewGuid()), CancellationToken.None);

        // Assert
        (await context.UserTokens.AnyAsync()).ShouldBeFalse();
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact]
    public void AddApplication_Should_RegisterHandlerByScan()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        ServiceDescriptor descriptor = services
            .Where(service => service.ServiceType == typeof(IDomainEventHandler<UserRegisteredDomainEvent>))
            .ShouldHaveSingleItem();
        descriptor.ImplementationType.ShouldBe(typeof(UserRegisteredDomainEventHandler));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    private UserRegisteredDomainEventHandler CreateHandler(TestDbContext context)
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.GenerateUserToken().Returns(ConfirmationToken);
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new UserRegisteredDomainEventHandler(
            context.UserRepository,
            context.UserTokenRepository,
            context.UnitOfWork,
            tokenProvider,
            dateTimeProvider,
            _emailSender);
    }
}
