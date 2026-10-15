using System.Diagnostics;
using Web.Api.Infrastructure;

namespace Web.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        // REMARK: If you want to use Controllers, you'll need this.
        services.AddControllers();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        // Every ProblemDetails (CustomResults and the exception handler) carries the trace id for correlation.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));

        return services;
    }
}
