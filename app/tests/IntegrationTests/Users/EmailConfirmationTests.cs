using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Application.Abstractions.Messaging;
using Application.Users.DeleteExpiredUnconfirmed;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace IntegrationTests.Users;

public sealed partial class EmailConfirmationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record ProblemDto(string Title, int Status, string? Detail);

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex Base64UrlToken();

    // Cadastro

    [Fact] // SPEC-0003 AC-1
    public async Task Register_Should_SendExactlyOneEmailWithTokenOnlyInBody()
    {
        // Arrange
        string email = UniqueEmail();

        // Act
        await RegisterUserAsync(email, confirmEmail: false);

        // Assert
        CapturingEmailSender.SentEmail sent = Factory.Emails.SentTo(email).ShouldHaveSingleItem();
        Base64UrlToken().IsMatch(sent.Token).ShouldBeTrue();
        sent.Subject.ShouldNotContain(sent.Token);
    }

    [Fact] // SPEC-0003 AC-2
    public async Task Register_Should_PersistOnlyTheTokenHash()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);
        string token = LastConfirmationToken(email);

        // Act
        UserToken stored = await QueryAsync(context => context.UserTokens.SingleAsync(t => t.UserId == userId));

        // Assert
        stored.TokenHash.ShouldNotBe(token);
        stored.TokenHash.ShouldBe(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))));
    }

    // Confirmação

    [Fact] // SPEC-0003 AC-3
    public async Task ConfirmEmail_Should_ReturnNoContentAndConfirm_WhenTokenIsValid()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);

        // Act
        HttpResponseMessage response = await PostConfirmAsync(LastConfirmationToken(email));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await EmailConfirmedAtAsync(userId)).ShouldNotBeNull();
    }

    [Fact] // SPEC-0003 AC-4
    public async Task ConfirmEmail_Should_ReturnSameInvalidTokenProblem_ForUnknownAndUsedTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email, confirmEmail: false);
        string token = LastConfirmationToken(email);
        await ConfirmEmailAsync(token);

        // Act
        HttpResponseMessage unknown = await PostConfirmAsync(new string('x', 43));
        HttpResponseMessage used = await PostConfirmAsync(token);

        // Assert
        ProblemDto unknownProblem = await ShouldBeInvalidTokenAsync(unknown);
        ProblemDto usedProblem = await ShouldBeInvalidTokenAsync(used);
        usedProblem.Detail.ShouldBe(unknownProblem.Detail);
    }

    [Fact] // SPEC-0003 AC-4, AC-5
    public async Task ConfirmEmail_Should_RejectEarlierToken_AfterAnotherTokenOfUserWasUsed()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);
        string firstToken = LastConfirmationToken(email);
        await MoveTokensBackAsync(userId, TimeSpan.FromSeconds(61));
        (await PostResendAsync(email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string secondToken = LastConfirmationToken(email);
        secondToken.ShouldNotBe(firstToken);

        // Act
        HttpResponseMessage second = await PostConfirmAsync(secondToken);
        HttpResponseMessage first = await PostConfirmAsync(firstToken);

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeInvalidTokenAsync(first);
    }

    [Fact] // SPEC-0003 AC-6
    public async Task ConfirmEmail_Should_AcceptTokenOnlyOnce_WhenUsedConcurrently()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email, confirmEmail: false);
        string token = LastConfirmationToken(email);

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(PostConfirmAsync(token), PostConfirmAsync(token));

        // Assert
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        await ShouldBeInvalidTokenAsync(responses.Single(r => r.StatusCode != HttpStatusCode.NoContent));
    }

    [Fact] // SPEC-0003 AC-7
    public async Task ConfirmEmail_Should_KeepOriginalInstant_WhenEmailIsAlreadyConfirmed()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);
        var originalInstant = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await ExecuteAsync(async context =>
        {
            User user = await context.Users.SingleAsync(u => u.Id == userId);
            user.ConfirmEmail(originalInstant);
            await context.SaveChangesAsync();
        });

        // Act
        HttpResponseMessage response = await PostConfirmAsync(LastConfirmationToken(email));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await EmailConfirmedAtAsync(userId)).ShouldBe(originalInstant);
    }

    [Theory] // SPEC-0003 AC-8a
    [InlineData("")]
    [InlineData(null)]
    public async Task ConfirmEmail_Should_ReturnBadRequest_WhenTokenIsMissing(string? token)
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/confirm-email", new { token });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact] // SPEC-0003 AC-8a
    public async Task ConfirmEmail_Should_ReturnValidationProblem_WhenTokenExceedsMaxLength()
    {
        // Act
        HttpResponseMessage response = await PostConfirmAsync(new string('a', 129));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Validation.General");
    }

    // Login

    [Fact] // SPEC-0003 AC-8
    public async Task Login_Should_ReturnForbiddenWithoutIssuingTokens_WhenEmailIsUnconfirmed()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = "Password123" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.EmailNotConfirmed");
        (await QueryAsync(context => context.RefreshTokens.AnyAsync(t => t.UserId == userId))).ShouldBeFalse();
    }

    [Fact] // SPEC-0003 AC-9
    public async Task Login_Should_ReturnTokens_WhenEmailWasConfirmed()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email, confirmEmail: false);
        await ConfirmEmailAsync(LastConfirmationToken(email));

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact] // SPEC-0003 AC-10
    public async Task Login_Should_ReturnInvalidCredentials_WhenEmailIsUnconfirmedAndPasswordIsWrong()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email, confirmEmail: false);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = "WrongPassword1" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.InvalidCredentials");
    }

    // Reenvio

    [Fact] // SPEC-0003 AC-11
    public async Task Resend_Should_SendNewWorkingToken_WhenLastTokenIsOlderThanSixtySeconds()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);
        await MoveTokensBackAsync(userId, TimeSpan.FromSeconds(61));

        // Act
        HttpResponseMessage response = await PostResendAsync($"  {email.ToUpperInvariant()} ");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Factory.Emails.SentTo(email).Count.ShouldBe(2);
        (await PostConfirmAsync(LastConfirmationToken(email))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact] // SPEC-0003 AC-12
    public async Task Resend_Should_AcceptWithoutSending_WhenLastTokenIsRecent()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email, confirmEmail: false);

        // Act
        HttpResponseMessage response = await PostResendAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Factory.Emails.SentTo(email).Count.ShouldBe(1);
    }

    [Fact] // SPEC-0003 AC-13
    public async Task Resend_Should_AnswerTheSame_WhenEmailIsUnknownOrAlreadyConfirmed()
    {
        // Arrange
        string unknownEmail = UniqueEmail();
        string confirmedEmail = UniqueEmail();
        Guid confirmedUserId = await RegisterUserAsync(confirmedEmail);
        await MoveTokensBackAsync(confirmedUserId, TimeSpan.FromSeconds(61));

        string unconfirmedEmail = UniqueEmail();
        Guid unconfirmedUserId = await RegisterUserAsync(unconfirmedEmail, confirmEmail: false);
        await MoveTokensBackAsync(unconfirmedUserId, TimeSpan.FromSeconds(61));

        // Act
        HttpResponseMessage unknown = await PostResendAsync(unknownEmail);
        HttpResponseMessage confirmed = await PostResendAsync(confirmedEmail);
        HttpResponseMessage unconfirmed = await PostResendAsync(unconfirmedEmail);

        // Assert
        foreach (HttpResponseMessage response in new[] { unknown, confirmed })
        {
            response.StatusCode.ShouldBe(unconfirmed.StatusCode);
            (await response.Content.ReadAsStringAsync()).ShouldBe(await unconfirmed.Content.ReadAsStringAsync());
        }

        unconfirmed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Factory.Emails.SentTo(unknownEmail).ShouldBeEmpty();
        Factory.Emails.SentTo(confirmedEmail).Count.ShouldBe(1);
    }

    [Theory] // SPEC-0003 AC-14
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Resend_Should_ReturnValidationProblem_WhenEmailIsInvalid(string email)
    {
        // Act
        HttpResponseMessage response = await PostResendAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Validation.General");
    }

    [Fact] // SPEC-0003 AC-14
    public async Task Resend_Should_ReturnValidationProblem_WhenEmailExceedsMaxLength()
    {
        // Act
        HttpResponseMessage response = await PostResendAsync($"{new string('a', 309)}@example.com");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Expiração do cadastro não confirmado

    [Fact] // SPEC-0003 AC-20
    public async Task DeleteExpiredUnconfirmed_Should_FreeTheEmail_AndKeepConfirmedUsers()
    {
        // Arrange
        string email = UniqueEmail();
        Guid expiredUserId = await RegisterUserAsync(email, confirmEmail: false);
        await MoveRegistrationBackAsync(expiredUserId, TimeSpan.FromDays(8));

        string confirmedEmail = UniqueEmail();
        Guid confirmedUserId = await RegisterUserAsync(confirmedEmail);
        await MoveRegistrationBackAsync(confirmedUserId, TimeSpan.FromDays(8));

        HttpResponseMessage beforeCleanup = await PostRegisterAsync(email);

        // Act
        Result<int> result = await DeleteExpiredUnconfirmedUsersAsync();
        HttpResponseMessage afterCleanup = await PostRegisterAsync(email);

        // Assert
        beforeCleanup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await beforeCleanup.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.EmailNotUnique");
        result.Value.ShouldBeGreaterThanOrEqualTo(1);
        afterCleanup.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await QueryAsync(context => context.Users.AnyAsync(u => u.Id == expiredUserId))).ShouldBeFalse();
        (await QueryAsync(context => context.UserTokens.AnyAsync(t => t.UserId == expiredUserId))).ShouldBeFalse();
        (await QueryAsync(context => context.Users.AnyAsync(u => u.Id == confirmedUserId))).ShouldBeTrue();
    }

    [Fact] // SPEC-0003 AC-21
    public async Task ConfirmEmail_Should_ReturnInvalidToken_WhenUserWasRemovedByExpiration()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);
        string token = LastConfirmationToken(email);
        await MoveRegistrationBackAsync(userId, TimeSpan.FromDays(8));
        await DeleteExpiredUnconfirmedUsersAsync();

        // Act
        HttpResponseMessage response = await PostConfirmAsync(token);

        // Assert
        await ShouldBeInvalidTokenAsync(response);
    }

    [Fact] // SPEC-0003 AC-22
    public void Host_Should_RegisterUnconfirmedUsersCleanupService()
    {
        // Act
        string[] hostedServices = [.. Factory.Services.GetServices<IHostedService>().Select(s => s.GetType().Name)];

        // Assert
        hostedServices.ShouldContain("UnconfirmedUsersCleanupService");
    }

    private Task<HttpResponseMessage> PostConfirmAsync(string token) =>
        HttpClient.PostAsJsonAsync("users/confirm-email", new { token });

    private Task<HttpResponseMessage> PostResendAsync(string email) =>
        HttpClient.PostAsJsonAsync("users/resend-email-confirmation", new { email });

    private Task<HttpResponseMessage> PostRegisterAsync(string email) =>
        HttpClient.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Test", lastName = "User", password = "Password123" });

    private static async Task<ProblemDto> ShouldBeInvalidTokenAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.InvalidEmailConfirmationToken");

        return problem;
    }

    private Task<DateTime?> EmailConfirmedAtAsync(Guid userId) =>
        QueryAsync(context => context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.EmailConfirmedAt)
            .SingleAsync());

    // Simulates elapsed time: the API reads the real clock, so the test moves the stored instants back.
    private Task MoveTokensBackAsync(Guid userId, TimeSpan elapsed) =>
        ExecuteAsync(context => context.UserTokens
            .Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                t => t.CreatedAtUtc,
                t => t.CreatedAtUtc.AddSeconds(-elapsed.TotalSeconds))));

    private Task MoveRegistrationBackAsync(Guid userId, TimeSpan elapsed) =>
        ExecuteAsync(context => context.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                u => u.RegisteredAt,
                u => u.RegisteredAt.AddSeconds(-elapsed.TotalSeconds))));

    private async Task<Result<int>> DeleteExpiredUnconfirmedUsersAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommandHandler<DeleteExpiredUnconfirmedUsersCommand, int> handler =
            scope.ServiceProvider.GetRequiredService<ICommandHandler<DeleteExpiredUnconfirmedUsersCommand, int>>();

        return await handler.Handle(new DeleteExpiredUnconfirmedUsersCommand(), CancellationToken.None);
    }

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await query(context);
    }

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task> action)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await action(context);
    }
}
