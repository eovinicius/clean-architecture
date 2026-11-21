using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Primitives;

namespace Api.Infrastructure;

// Opt-in idempotency for a POST action ([Idempotent]). When a request carries an
// Idempotency-Key header, the first request for that key is processed and the
// key is remembered; a repeat with the same key returns 409 Conflict. The key is
// scoped to the user, HTTP method and route, and released again when the request
// fails (exception or non-2xx response) so a legitimate retry is processed.
// Requests without the header are processed normally. For full "replay the
// original response" idempotency you would additionally store and return the first result.
// A resource filter wraps the result execution, so the final status code is known when it returns.
internal sealed class IdempotencyFilter(HybridCache cache, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncResourceFilter
{
    internal const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1)
    };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        HttpContext httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out StringValues values) ||
            values.FirstOrDefault() is not { Length: > 0 } idempotencyKey)
        {
            // No idempotency key supplied - process the request normally.
            await next();
            return;
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
            ProblemDetails problem = problemDetailsFactory.CreateProblemDetails(
                httpContext,
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate request",
                detail: "A request with this idempotency key has already been processed.");

            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentTypes = { MediaTypeNames.Application.ProblemJson }
            };
            return;
        }

        ResourceExecutedContext executed;

        try
        {
            executed = await next();
        }
        catch
        {
            await cache.RemoveAsync(cacheKey, CancellationToken.None);
            throw;
        }

        bool failed = executed.Exception is not null && !executed.ExceptionHandled;

        if (failed || !IsSuccessStatusCode(httpContext.Response.StatusCode))
        {
            await cache.RemoveAsync(cacheKey, CancellationToken.None);
        }
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
