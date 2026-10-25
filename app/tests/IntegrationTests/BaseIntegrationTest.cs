using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        HttpClient = factory.CreateClient();
    }

    // Lets tests spin up a host with overridden settings via Factory.WithWebHostBuilder(...).
    protected IntegrationTestWebAppFactory Factory { get; }

    protected HttpClient HttpClient { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    // Confirms the email by default (SPEC-0003): an unconfirmed user cannot log in.
    protected async Task<Guid> RegisterUserAsync(string email, bool confirmEmail = true)
    {
        var request = new
        {
            email,
            firstName = "Test",
            lastName = "User",
            password = "Password123"
        };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);
        response.EnsureSuccessStatusCode();

        Guid userId = await response.Content.ReadFromJsonAsync<Guid>();

        if (confirmEmail)
        {
            await ConfirmEmailAsync(LastConfirmationToken(email));
        }

        return userId;
    }

    protected string LastConfirmationToken(string email) => Factory.Emails.SentTo(email)[^1].Token;

    protected async Task ConfirmEmailAsync(string token)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/confirm-email", new { token });
        response.EnsureSuccessStatusCode();
    }

    protected async Task<AccessTokens> LoginAsync(string email)
    {
        var request = new { email, password = "Password123" };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", request);
        response.EnsureSuccessStatusCode();

        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!;
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
