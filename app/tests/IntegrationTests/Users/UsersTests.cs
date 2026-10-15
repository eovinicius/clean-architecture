using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Register_Should_ReturnUserId()
    {
        // Act
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Assert
        userId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetUser_Should_ReturnNotFound_WhenIdIsNotAGuid()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/abc");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenFirstNameExceedsMaxLength()
    {
        // Arrange
        var request = new
        {
            email = UniqueEmail(),
            firstName = new string('a', 101),
            lastName = "User",
            password = "Password123"
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Validation.General");
    }

    [Fact]
    public async Task Login_Should_ReturnAccessAndRefreshTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_ReturnInvalidCredentials_WhenPasswordIsInvalid()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = "WrongPassword1" });

        // Assert
        await ShouldBeInvalidCredentialsAsync(response);
    }

    [Fact]
    public async Task Login_Should_ReturnInvalidCredentials_WhenEmailDoesNotExist()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email = UniqueEmail(), password = "Password123" });

        // Assert
        await ShouldBeInvalidCredentialsAsync(response);
    }

    [Fact]
    public async Task Login_Should_ReturnTokens_WhenEmailCasingDiffers()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email = email.ToUpperInvariant(), password = "Password123" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RefreshToken_Should_RevokeFamily_WhenRotatedTokenIsReused()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens original = await LoginAsync(email);

        HttpResponseMessage firstRefresh = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = original.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();
        AccessTokens rotated = (await firstRefresh.Content.ReadFromJsonAsync<AccessTokens>())!;

        // Act
        HttpResponseMessage reuse = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = original.RefreshToken });
        HttpResponseMessage afterReuse = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = rotated.RefreshToken });

        // Assert
        reuse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnNewTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });

        // Assert
        response.EnsureSuccessStatusCode();
        AccessTokens? rotated = await response.Content.ReadFromJsonAsync<AccessTokens>();
        rotated!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task GetUserById_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_Should_ReturnUser_WhenAuthenticated()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnProblem_WhenTokenIsInvalid()
    {
        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = "this-token-does-not-exist" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record ProblemDto(string Title, int Status);

    private static async Task ShouldBeInvalidCredentialsAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.InvalidCredentials");
    }
}
