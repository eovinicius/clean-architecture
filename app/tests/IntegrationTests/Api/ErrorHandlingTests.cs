using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Todos.Get;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Web.Api;

namespace IntegrationTests.Api;

public sealed class ErrorHandlingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string InternalSecret = "internal-secret-detail";

    [Fact]
    public async Task CreateTodo_Should_ReturnBadRequest_WhenBodyIsMalformedJson()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync("todos", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnhandledException_Should_ReturnServerFailure_WithSecurityHeadersAndTraceId()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();

        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IQueryHandler<GetTodosQuery, PagedList<TodoResponse>>, ThrowingGetTodosHandler>()));
        using HttpClient client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Act
        HttpResponseMessage response = await client.GetAsync($"todos?userId={userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);

        string body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(InternalSecret);

        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Server failure");
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidationError_Should_IncludeTraceId()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"todos?userId={userId}&page=1&pageSize=1000");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    private sealed record ProblemDto(string Title, int Status, string? TraceId);

    private sealed class ThrowingGetTodosHandler : IQueryHandler<GetTodosQuery, PagedList<TodoResponse>>
    {
        public Task<Result<PagedList<TodoResponse>>> Handle(GetTodosQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(InternalSecret);
    }
}
