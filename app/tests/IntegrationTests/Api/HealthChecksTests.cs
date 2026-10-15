using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.Api;

namespace IntegrationTests.Api;

public sealed class HealthChecksTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("health/live")]
    [InlineData("health/ready")]
    [InlineData("health")]
    public async Task HealthCheck_Should_ReturnOk(string route)
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(route);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessCheck_Should_IncludeDatabase()
    {
        // Act
        string body = await HttpClient.GetStringAsync("health/ready");

        // Assert
        body.ShouldContain("npgsql");
    }

    [Fact]
    public async Task LivenessCheck_Should_NotBeRateLimited()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1"));
        using HttpClient client = app.CreateClient();

        // Act
        HttpResponseMessage[] responses =
        [
            await client.GetAsync("health/live"),
            await client.GetAsync("health/live"),
            await client.GetAsync("health/live")
        ];

        // Assert
        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
    }
}
