using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.Api;

namespace IntegrationTests.Api;

public sealed class RateLimitingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Login_Should_ReturnTooManyRequests_WhenAuthenticationLimitIsExceeded()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "2"));
        using HttpClient client = app.CreateClient();
        var request = new { email = UniqueEmail(), password = "Password123" };

        // Act
        HttpResponseMessage first = await client.PostAsJsonAsync("users/login", request);
        HttpResponseMessage second = await client.PostAsJsonAsync("users/login", request);
        HttpResponseMessage third = await client.PostAsJsonAsync("users/login", request);

        // Assert
        first.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GetTodos_Should_NotThrottleOtherUser_WhenOneUserExhaustsGlobalLimit()
    {
        // Arrange
        // Users are created through the default host so the throttled host only sees authenticated traffic.
        (Guid firstUserId, AccessTokens firstTokens) = await RegisterAndLoginAsync();
        (Guid secondUserId, AccessTokens secondTokens) = await RegisterAndLoginAsync();

        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Global:PermitLimit", "2"));
        using HttpClient client = app.CreateClient();

        // Act
        HttpResponseMessage[] firstUserResponses =
        [
            await GetTodosAsync(client, firstUserId, firstTokens.AccessToken),
            await GetTodosAsync(client, firstUserId, firstTokens.AccessToken),
            await GetTodosAsync(client, firstUserId, firstTokens.AccessToken)
        ];
        HttpResponseMessage secondUserResponse = await GetTodosAsync(client, secondUserId, secondTokens.AccessToken);

        // Assert
        firstUserResponses[0].StatusCode.ShouldBe(HttpStatusCode.OK);
        firstUserResponses[1].StatusCode.ShouldBe(HttpStatusCode.OK);
        firstUserResponses[2].StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        secondUserResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> GetTodosAsync(HttpClient client, Guid userId, string accessToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"todos?userId={userId}&page=1&pageSize=10");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await client.SendAsync(message);
    }
}
