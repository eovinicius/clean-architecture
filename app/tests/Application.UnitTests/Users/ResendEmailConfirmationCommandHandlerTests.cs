using Application.Abstractions.Authentication;
using Application.Abstractions.Notifications;
using Application.UnitTests.Abstractions;
using Application.Users.ResendEmailConfirmation;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Users;

public sealed class ResendEmailConfirmationCommandHandlerTests : BaseHandlerTest
{
    private const string Email = "test@example.com";
    private const string NewToken = "new-token";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    [Fact] // SPEC-0003 AC-11
    public async Task Handle_Should_IssueAndSendNewToken_WhenEmailIsUnconfirmedAndLastTokenIsOlderThanSixtySeconds()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, lastTokenIssuedAt: Now.AddSeconds(-61));
        ResendEmailConfirmationCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(
            new ResendEmailConfirmationCommand("  Test@Example.COM "),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.CountAsync(t => t.UserId == user.Id)).ShouldBe(2);
        (await context.UserTokens.AnyAsync(t => t.TokenHash == $"hash:{NewToken}")).ShouldBeTrue();
        await _emailSender.Received(1).SendAsync(
            Email,
            Arg.Is<string>(subject => !subject.Contains(NewToken, StringComparison.Ordinal)),
            Arg.Is<string>(body => body.Contains(NewToken, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact] // SPEC-0003 AC-12
    public async Task Handle_Should_NotSend_WhenLastTokenWasIssuedWithinSixtySeconds()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, lastTokenIssuedAt: Now.AddSeconds(-60));
        ResendEmailConfirmationCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ResendEmailConfirmationCommand(Email), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.CountAsync(t => t.UserId == user.Id)).ShouldBe(1);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact] // SPEC-0003 AC-13
    public async Task Handle_Should_SucceedWithoutSending_WhenEmailIsNotRegistered()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        ResendEmailConfirmationCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ResendEmailConfirmationCommand(Email), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.AnyAsync()).ShouldBeFalse();
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact] // SPEC-0003 AC-13
    public async Task Handle_Should_SucceedWithoutSending_WhenEmailIsAlreadyConfirmed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, lastTokenIssuedAt: Now.AddDays(-1));
        user.ConfirmEmail(Now.AddDays(-1));
        await context.SaveChangesAsync();
        ResendEmailConfirmationCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new ResendEmailConfirmationCommand(Email), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.UserTokens.CountAsync(t => t.UserId == user.Id)).ShouldBe(1);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }

    [Fact] // SPEC-0003 RN-5
    public async Task Handle_Should_KeepPreviousValidToken_WhenIssuingNewOne()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, lastTokenIssuedAt: Now.AddMinutes(-5));
        ResendEmailConfirmationCommandHandler handler = CreateHandler(context);

        // Act
        await handler.Handle(new ResendEmailConfirmationCommand(Email), CancellationToken.None);

        // Assert
        (await context.UserTokens.AnyAsync(t => t.UserId == user.Id && t.TokenHash == "hash:previous")).ShouldBeTrue();
    }

    [Fact] // SPEC-0003 seção 7 (limpeza)
    public async Task Handle_Should_RemoveExpiredTokensOfUser_WhenIssuingNewOne()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User user = await SeedUserAsync(context, lastTokenIssuedAt: Now.AddDays(-2));
        ResendEmailConfirmationCommandHandler handler = CreateHandler(context);

        // Act
        await handler.Handle(new ResendEmailConfirmationCommand(Email), CancellationToken.None);

        // Assert
        (await context.UserTokens.Where(t => t.UserId == user.Id).Select(t => t.TokenHash).ToListAsync())
            .ShouldBe([$"hash:{NewToken}"]);
    }

    private ResendEmailConfirmationCommandHandler CreateHandler(TestDbContext context)
    {
        ITokenProvider tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.GenerateUserToken().Returns(NewToken);
        tokenProvider.HashUserToken(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");

        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(Now);

        return new ResendEmailConfirmationCommandHandler(context, tokenProvider, dateTimeProvider, _emailSender);
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, DateTime lastTokenIssuedAt)
    {
        var user = User.Create(Email, "Test", "User", "hash", lastTokenIssuedAt);
        context.Users.Add(user);
        context.UserTokens.Add(
            UserToken.Issue(user.Id, UserTokenPurpose.EmailConfirmation, "hash:previous", lastTokenIssuedAt));

        await context.SaveChangesAsync();
        user.ClearDomainEvents();

        return user;
    }
}
