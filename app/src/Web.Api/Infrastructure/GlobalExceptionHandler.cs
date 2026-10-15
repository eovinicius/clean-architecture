using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Web.Api.Infrastructure;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    // Non-standard "Client Closed Request" (nginx convention): nobody receives it, it only labels logs and metrics.
    private const int StatusClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request aborted by the client");

            httpContext.Response.StatusCode = StatusClientClosedRequest;

            return true;
        }

        ProblemDetails problemDetails;

        if (exception is BadHttpRequestException badRequestException)
        {
            logger.LogWarning(exception, "Bad request");

            problemDetails = new ProblemDetails
            {
                Status = badRequestException.StatusCode,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Bad request"
            };
        }
        else
        {
            logger.LogError(exception, "Unhandled exception occurred");

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "Server failure"
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
