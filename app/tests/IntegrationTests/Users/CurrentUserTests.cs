using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed class CurrentUserTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record CurrentUserDto(Guid Id, string Email, string FirstName, string LastName);

    private sealed record ProblemDto(string Title, int Status);

    [Fact] // SPEC-0002 AC-1
    public async Task GetCurrentUser_Should_ReturnRegisteredData()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CurrentUserDto? user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        user!.Id.ShouldBe(userId);
        user.Email.ShouldBe(email);
        user.FirstName.ShouldBe("Test");
        user.LastName.ShouldBe("User");
    }

    [Fact] // SPEC-0002 AC-2
    public async Task GetCurrentUser_Should_ReturnOnlyOwnData_WhenAnotherUserExists()
    {
        // Arrange
        (Guid userAId, AccessTokens tokensA) = await RegisterAndLoginAsync();
        (Guid userBId, _) = await RegisterAndLoginAsync();
        Authenticate(tokensA.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CurrentUserDto? user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        user!.Id.ShouldBe(userAId);
        user.Id.ShouldNotBe(userBId);
    }

    [Fact] // SPEC-0002 AC-3
    public async Task GetCurrentUser_Should_IgnoreUserIdInQueryString()
    {
        // Arrange
        (Guid userAId, AccessTokens tokensA) = await RegisterAndLoginAsync();
        (Guid userBId, _) = await RegisterAndLoginAsync();
        Authenticate(tokensA.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/me?userId={userBId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CurrentUserDto? user = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        user!.Id.ShouldBe(userAId);
    }

    [Fact] // SPEC-0002 AC-5
    public async Task GetCurrentUser_Should_ReturnNotFound_WhenUserWasRemovedAfterLogin()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
    }

    [Fact] // SPEC-0002 AC-6
    public async Task GetCurrentUser_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact] // SPEC-0002 AC-7
    public async Task GetCurrentUser_Should_ReturnUnauthorized_WhenTokenIsMalformed()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact] // SPEC-0002 AC-8
    public async Task GetCurrentUser_Should_CoexistWithGetUserById()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage byIdResponse = await HttpClient.GetAsync($"users/{userId}");
        HttpResponseMessage meResponse = await HttpClient.GetAsync("users/me");

        // Assert
        byIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        CurrentUserDto? byId = await byIdResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        CurrentUserDto? me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        byId!.Id.ShouldBe(userId);
        me!.Id.ShouldBe(userId);
    }
}
