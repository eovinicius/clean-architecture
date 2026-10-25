using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Api;

// Guards .claude/rules/60-security.md: every endpoint requires authorization unless it is anonymous by design.
public sealed class EndpointAuthorizationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly string[] AnonymousRoutes =
    [
        "users/login",
        "users/register",
        "users/refresh-token",
        "users/confirm-email", // SPEC-0003 AC-16: confirmar acontece antes do primeiro login.
        "users/resend-email-confirmation" // SPEC-0003 AC-16
    ];

    private static readonly string[] AnonymousRoutePrefixes =
    [
        "health",
        "openapi",
        "scalar"
    ];

    [Fact]
    public void Endpoints_Should_RequireAuthorization_UnlessAnonymousByDesign()
    {
        // Arrange
        RouteEndpoint[] endpoints = [.. Factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
        endpoints.ShouldNotBeEmpty();

        // Act
        string[] violations = [.. endpoints
            .Where(endpoint => !IsAnonymousByDesign(RouteOf(endpoint)) && !RequiresAuthorization(endpoint))
            .Select(endpoint => $"{string.Join(',', endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])} {RouteOf(endpoint)}")];

        // Assert
        violations.ShouldBeEmpty();
    }

    private static string RouteOf(RouteEndpoint endpoint) => endpoint.RoutePattern.RawText?.TrimStart('/') ?? string.Empty;

    private static bool IsAnonymousByDesign(string route) =>
        AnonymousRoutes.Contains(route, StringComparer.Ordinal) ||
        AnonymousRoutePrefixes.Any(prefix => route.StartsWith(prefix, StringComparison.Ordinal));

    private static bool RequiresAuthorization(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null &&
        endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
}
