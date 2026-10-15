using Infrastructure.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Web.Api;

namespace IntegrationTests.Api;

public sealed class StartupConfigurationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("Jwt:Secret", "")]
    [InlineData("Jwt:RefreshTokenExpirationInDays", "0")]
    public async Task Startup_Should_Fail_WhenJwtConfigurationIsInvalid(string key, string value)
    {
        // Arrange
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        // Act
        Exception? exception = Record.Exception(() => app.CreateClient());

        // Assert
        OptionsValidationException? validationException = FindValidationException(exception);
        validationException.ShouldNotBeNull();
        validationException.OptionsType.ShouldBe(typeof(JwtOptions));
    }

    [Fact]
    public async Task Startup_Should_Fail_WhenRateLimitingConfigurationIsInvalid()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Global:PermitLimit", "0"));

        // Act
        Exception? exception = Record.Exception(() => app.CreateClient());

        // Assert
        FindValidationException(exception).ShouldNotBeNull();
    }

    // ValidateOnStart may surface the failure directly or wrapped (e.g. in an AggregateException).
    private static OptionsValidationException? FindValidationException(Exception? exception) =>
        exception switch
        {
            null => null,
            OptionsValidationException validationException => validationException,
            AggregateException aggregate => aggregate.InnerExceptions
                .Select(FindValidationException)
                .FirstOrDefault(e => e is not null),
            _ => FindValidationException(exception.InnerException)
        };
}
