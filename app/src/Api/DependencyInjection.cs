using System.Diagnostics;
using Api.BackgroundJobs;
using Api.Extensions;
using Api.Infrastructure;

namespace Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddControllersWithJsonContract();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        // Every ProblemDetails (CustomResults and the exception handler) carries the trace id for correlation.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));

        services.AddHostedService<UnconfirmedUsersCleanupService>();

        return services;
    }
}
