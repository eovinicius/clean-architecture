using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Web.Api.Infrastructure;

namespace Web.Api.Extensions;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddRateLimitingInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((options, rateLimitingOptions) =>
            {
                FixedWindowSettings global = rateLimitingOptions.Value.Global;
                FixedWindowSettings authentication = rateLimitingOptions.Value.Authentication;

                // A global fixed-window limiter, partitioned by authenticated user or client IP.
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => ToLimiterOptions(global)));

                // A stricter policy for authentication endpoints to slow down brute-force attempts.
                options.AddPolicy(RateLimitingPolicies.Authentication, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => ToLimiterOptions(authentication)));
            });

        return services;
    }

    private static FixedWindowRateLimiterOptions ToLimiterOptions(FixedWindowSettings settings) =>
        new()
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowInSeconds)
        };

    // Prefixes keep a user id from ever colliding with an IP address.
    private static string GetPartitionKey(HttpContext httpContext)
    {
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return userId is not null
            ? $"user:{userId}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
