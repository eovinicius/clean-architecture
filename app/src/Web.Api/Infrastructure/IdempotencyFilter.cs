using System.Security.Claims;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Primitives;

namespace Web.Api.Infrastructure;

// Opt-in idempotency for a POST endpoint. When a request carries an
// Idempotency-Key header, the first request for that key is processed and the
// key is remembered; a repeat with the same key returns 409 Conflict. The key is
// scoped to the user, HTTP method and route, and released again when the request
// fails (exception or non-2xx response) so a legitimate retry is processed.
// Requests without the header are processed normally. For full "replay the
// original response" idempotency you would additionally store and return the first result.
internal sealed class IdempotencyFilter(HybridCache cache) : IEndpointFilter
{
    internal const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1)
    };

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out StringValues values) ||
            values.FirstOrDefault() is not { Length: > 0 } idempotencyKey)
        {
            // No idempotency key supplied - process the request normally.
            return await next(context);
        }

        string cacheKey = BuildCacheKey(httpContext, idempotencyKey);

        bool isFirstRequest = false;

        await cache.GetOrCreateAsync(
            cacheKey,
            _ =>
            {
                isFirstRequest = true;
                return ValueTask.FromResult(true);
            },
            CacheOptions,
            cancellationToken: httpContext.RequestAborted);

        if (!isFirstRequest)
        {
            return Results.Problem(
                title: "Duplicate request",
                detail: "A request with this idempotency key has already been processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        object? result;

        try
        {
            result = await next(context);
        }
        catch
        {
            await cache.RemoveAsync(cacheKey, CancellationToken.None);
            throw;
        }

        // Results without a status code are serialized as 200 by minimal APIs.
        if (result is IStatusCodeHttpResult { StatusCode: { } statusCode } && !IsSuccessStatusCode(statusCode))
        {
            await cache.RemoveAsync(cacheKey, CancellationToken.None);
        }

        return result;
    }

    private static string BuildCacheKey(HttpContext httpContext, string idempotencyKey)
    {
        string userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

        string route = (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
            ?? httpContext.Request.Path.ToString();

        return $"idempotency:{userId}:{httpContext.Request.Method}:{route}:{idempotencyKey}";
    }

    private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;
}
