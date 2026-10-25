using Application.Abstractions.Authentication;
using Application.Abstractions.Notifications;
using Application.UnitTests.Abstractions;
using Application.Users.RequestPasswordReset;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class RequestPasswordResetCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "test@example.com";
    private const string ResetToken = "reset-token";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    [Fact] // SPEC-0004 AC-1, AC-3
    public async Task Handle_Should_PersistHashAndSendTokenOnlyInBody_WhenUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        RequestPasswordResetCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new RequestPasswordResetCommand("  Test@Example.COM "), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        UserToken token = await context.UserTokens.SingleAsync(t => t.UserId == user.Id);
        token.Purpose.ShouldBe(UserTokenPurpose.PasswordReset);
        token.TokenHash.ShouldBe($"hash:{ResetToken}");
        token.ExpiresOnUtc.ShouldBe(Now.AddHours(1));
        await _emailSender.Received(1).SendAsync(
            Email,
            Arg.Is<string>(subject => !subject.Contains(ResetToken, StringComparison.Ordinal)),
            Arg.Is<string>(body => body.Contains(ResetToken, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact] // SPEC-0004 AC-2
    public async Task Handle_Should_SucceedWithoutSending_WhenEmailIsNotRegistered()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        RequestPasswordResetCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new RequestPasswordResetCommand(Email), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.AnyAsync()).ShouldBeFalse();
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact] // SPEC-0004 AC-4
    public async Task Handle_Should_NotSendAgain_WhenLastResetTokenIsWithinSixtySeconds()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        context.UserTokens.Add(UserToken.Issue(user.Id, UserTokenPurpose.PasswordReset, "hash:previous", Now.AddSeconds(-60)));
        await context.SaveChangesAsync();
        RequestPasswordResetCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new RequestPasswordResetCommand(Email), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.CountAsync(t => t.UserId == user.Id)).ShouldBe(1);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact] // SPEC-0004 RN-6
    public async Task Handle_Should_IgnoreRecentConfirmationToken_WhenCheckingTheInterval()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context);
        context.UserTokens.Add(UserToken.Issue(user.Id, UserTokenPurpose.EmailConfirmation, "hash:confirmation", Now));
        await context.SaveChangesAsync();
        RequestPasswordResetCommandHandler handler = CreateHandler(context);

        // Act
        await handler.Handle(new RequestPasswordResetCommand(Email), CancellationToken.None);

        // Assert
        (await context.UserTokens.CountAsync(t => t.UserId == user.Id)).ShouldBe(2);
        await _emailSender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, default!, default);
    }

    [Fact] // SPEC-0004 AC-11 (RN-8)
    public async Task Handle_Should_SendToken_WhenEmailIsNotConfirmed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedUserAsync(context, confirmEmail: false);
        RequestPasswordResetCommandHandler handler = CreateHandler(context);

        // Act
        await handler.Handle(new RequestPasswordResetCommand(Email), CancellationToken.None);

        // Assert
        await _emailSender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, default!, default);
    }

    private RequestPasswordResetCommandHandler CreateHandler(TestDbContext context)
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.GenerateUserToken().Returns(ResetToken);
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new RequestPasswordResetCommandHandler(context, tokenProvider, dateTimeProvider, _emailSender);
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool confirmEmail = true)
    {
        var user = User.Create(Email, "Test", "User", "hash", Now.AddDays(-1));
        if (confirmEmail)
        {
            user.ConfirmEmail(Now.AddDays(-1));
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        user.ClearDomainEvents();

        return user;
    }
}
