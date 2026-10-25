using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed partial class PasswordResetTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string NewPassword = "NewPassword123";

    private sealed record ProblemDto(string Title, int Status, string? Detail);

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex Base64UrlToken();

    // Solicitação

    [Fact] // SPEC-0004 AC-1
    public async Task ForgotPassword_Should_SendExactlyOneResetEmail_WithTokenOnlyInBody()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        int emailsBefore = Factory.Emails.SentTo(email).Count;

        // Act
        HttpResponseMessage response = await PostForgotAsync($"  {email.ToUpperInvariant()} ");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Factory.Emails.SentTo(email).Count.ShouldBe(emailsBefore + 1);
        CapturingEmailSender.SentEmail sent = Factory.Emails.SentTo(email)[^1];
        Base64UrlToken().IsMatch(sent.Token).ShouldBeTrue();
        sent.Subject.ShouldNotContain(sent.Token);
    }

    [Fact] // SPEC-0004 AC-2
    public async Task ForgotPassword_Should_AnswerTheSame_WhenEmailIsNotRegistered()
    {
        // Arrange
        string registered = UniqueEmail();
        await RegisterUserAsync(registered);
        string unknown = UniqueEmail();

        // Act
        HttpResponseMessage registeredResponse = await PostForgotAsync(registered);
        HttpResponseMessage unknownResponse = await PostForgotAsync(unknown);

        // Assert
        unknownResponse.StatusCode.ShouldBe(registeredResponse.StatusCode);
        (await unknownResponse.Content.ReadAsStringAsync())
            .ShouldBe(await registeredResponse.Content.ReadAsStringAsync());
        Factory.Emails.SentTo(unknown).ShouldBeEmpty();
    }

    [Fact] // SPEC-0004 AC-3
    public async Task ForgotPassword_Should_PersistOnlyTheTokenHash_WithResetPurpose()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        // Act
        await PostForgotAsync(email);
        string token = LastToken(email);

        // Assert
        UserToken stored = await QueryAsync(context => context.UserTokens
            .SingleAsync(t => t.UserId == userId && t.Purpose == UserTokenPurpose.PasswordReset));
        stored.TokenHash.ShouldNotBe(token);
        stored.TokenHash.ShouldBe(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))));
    }

    [Fact] // SPEC-0004 AC-4
    public async Task ForgotPassword_Should_NotSendAgain_WithinSixtySeconds()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        await PostForgotAsync(email);
        int emailsAfterFirst = Factory.Emails.SentTo(email).Count;

        // Act
        HttpResponseMessage response = await PostForgotAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Factory.Emails.SentTo(email).Count.ShouldBe(emailsAfterFirst);
    }

    [Theory] // SPEC-0004 AC-4a
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task ForgotPassword_Should_ReturnValidationProblem_WhenEmailIsInvalid(string email)
    {
        // Act
        HttpResponseMessage response = await PostForgotAsync(email);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Validation.General");
    }

    [Fact] // SPEC-0004 AC-4a
    public async Task ForgotPassword_Should_ReturnValidationProblem_WhenEmailExceedsMaxLength()
    {
        // Act
        HttpResponseMessage response = await PostForgotAsync($"{new string('a', 309)}@example.com");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // Redefinição

    [Fact] // SPEC-0004 AC-5
    public async Task ResetPassword_Should_ReplacePassword_WhenTokenIsValid()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        await PostForgotAsync(email);

        // Act
        HttpResponseMessage response = await PostResetAsync(LastToken(email), NewPassword);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PostLoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage oldPassword = await PostLoginAsync(email, "Password123");
        oldPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await oldPassword.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.InvalidCredentials");
    }

    [Fact] // SPEC-0004 AC-6
    public async Task ResetPassword_Should_ReturnSameInvalidTokenProblem_ForUnknownAndUsedTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        await PostForgotAsync(email);
        string token = LastToken(email);
        (await PostResetAsync(token, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage unknown = await PostResetAsync(new string('x', 43), "AnotherPassword123");
        HttpResponseMessage used = await PostResetAsync(token, "AnotherPassword123");

        // Assert
        ProblemDto unknownProblem = await ShouldBeInvalidTokenAsync(unknown);
        ProblemDto usedProblem = await ShouldBeInvalidTokenAsync(used);
        usedProblem.Detail.ShouldBe(unknownProblem.Detail);
        (await PostLoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact] // SPEC-0004 AC-6 (RN-5)
    public async Task ResetPassword_Should_RejectEarlierToken_AfterAnotherTokenOfUserWasUsed()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await PostForgotAsync(email);
        string firstToken = LastToken(email);
        await MoveResetTokensBackAsync(userId, TimeSpan.FromSeconds(61));
        await PostForgotAsync(email);
        string secondToken = LastToken(email);
        secondToken.ShouldNotBe(firstToken);

        // Act
        HttpResponseMessage second = await PostResetAsync(secondToken, NewPassword);
        HttpResponseMessage first = await PostResetAsync(firstToken, "AnotherPassword123");

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShouldBeInvalidTokenAsync(first);
        (await PostLoginAsync(email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact] // SPEC-0004 AC-7
    public async Task ResetPassword_Should_AcceptTokenOnlyOnce_WhenUsedConcurrently()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        await PostForgotAsync(email);
        string token = LastToken(email);

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            PostResetAsync(token, "FirstPassword123"),
            PostResetAsync(token, "SecondPassword123"));

        // Assert
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        await ShouldBeInvalidTokenAsync(responses.Single(r => r.StatusCode != HttpStatusCode.NoContent));

        string winner = responses[0].StatusCode == HttpStatusCode.NoContent ? "FirstPassword123" : "SecondPassword123";
        string loser = winner == "FirstPassword123" ? "SecondPassword123" : "FirstPassword123";
        (await PostLoginAsync(email, winner)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostLoginAsync(email, loser)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory] // SPEC-0004 AC-8
    [InlineData(null, NewPassword)]
    [InlineData("x", "")]
    [InlineData("x", "Pass123")]
    [InlineData("x", "129")]
    [InlineData("129", NewPassword)]
    public async Task ResetPassword_Should_ReturnValidationProblem_AndKeepTokenUsable(string? tokenCase, string passwordCase)
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        await PostForgotAsync(email);
        string token = LastToken(email);

        string? sentToken = tokenCase switch
        {
            null => null,
            "129" => new string('a', 129),
            _ => token
        };
        string sentPassword = passwordCase == "129" ? new string('p', 129) : passwordCase;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/reset-password",
            new { token = sentToken, newPassword = sentPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostResetAsync(token, new string('p', 128))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact] // SPEC-0004 AC-8 (RN-9)
    public async Task ResetPassword_Should_AcceptEightCharacterPassword()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        await PostForgotAsync(email);

        // Act
        HttpResponseMessage response = await PostResetAsync(LastToken(email), "Pass1234");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // Sessões

    [Fact] // SPEC-0004 AC-9
    public async Task ResetPassword_Should_RevokeEverySession()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens firstSession = await LoginAsync(email);
        AccessTokens secondSession = await LoginAsync(email);
        await PostForgotAsync(email);

        // Act
        (await PostResetAsync(LastToken(email), NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert
        foreach (AccessTokens session in new[] { firstSession, secondSession })
        {
            HttpResponseMessage refresh = await HttpClient.PostAsJsonAsync(
                "users/refresh-token",
                new { refreshToken = session.RefreshToken });
            refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await refresh.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.InvalidRefreshToken");
        }
    }

    // E-mail não confirmado

    [Fact] // SPEC-0004 AC-11
    public async Task ResetPassword_Should_NotConfirmEmail_WhenEmailIsUnconfirmed()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email, confirmEmail: false);

        // Act
        HttpResponseMessage forgot = await PostForgotAsync(email);
        HttpResponseMessage reset = await PostResetAsync(LastToken(email), NewPassword);

        // Assert
        forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage login = await PostLoginAsync(email, NewPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await login.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.EmailNotConfirmed");
        (await QueryAsync(context => context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.EmailConfirmedAt)
            .SingleAsync())).ShouldBeNull();
    }

    private string LastToken(string email) => Factory.Emails.SentTo(email)[^1].Token;

    private Task<HttpResponseMessage> PostForgotAsync(string email) =>
        HttpClient.PostAsJsonAsync("users/forgot-password", new { email });

    private Task<HttpResponseMessage> PostResetAsync(string token, string newPassword) =>
        HttpClient.PostAsJsonAsync("users/reset-password", new { token, newPassword });

    private Task<HttpResponseMessage> PostLoginAsync(string email, string password) =>
        HttpClient.PostAsJsonAsync("users/login", new { email, password });

    private static async Task<ProblemDto> ShouldBeInvalidTokenAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.InvalidPasswordResetToken");

        return problem;
    }

    // Simulates elapsed time: the API reads the real clock, so the test moves the stored instants back.
    private async Task MoveResetTokensBackAsync(Guid userId, TimeSpan elapsed)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == UserTokenPurpose.PasswordReset)
            .ExecuteUpdateAsync(s => s.SetProperty(
                t => t.CreatedAtUtc,
                t => t.CreatedAtUtc.AddSeconds(-elapsed.TotalSeconds)));
    }

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await query(context);
    }
}
