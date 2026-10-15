namespace Web.Api.Middleware;

// Adds a conservative set of security response headers to every response.
// Applied in OnStarting so they survive the exception handler clearing the response.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            return Task.CompletedTask;
        });

        return next(context);
    }
}
