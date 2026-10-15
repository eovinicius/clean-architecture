using System.Globalization;
using System.Reflection;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Web.Api;
using Web.Api.Extensions;

// REMARK: Captures startup failures before the configured logger exists.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddOpenApiWithAuth();

    builder.Services
        .AddApplication()
        .AddPresentation()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

    builder.Services.AddRateLimitingInternal(builder.Configuration);

    builder.Services.AddCorsInternal(builder.Configuration);

    builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

    WebApplication app = builder.Build();

    app.MapEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApiWithScalar();

        app.ApplyMigrations();
    }

    // REMARK: Probes are excluded from rate limiting so orchestrators are never throttled.
    var readinessOptions = new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    };

    app.MapHealthChecks("health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).DisableRateLimiting();

    app.MapHealthChecks("health/ready", readinessOptions).DisableRateLimiting();

    // REMARK: Legacy alias of health/ready.
    app.MapHealthChecks("health", readinessOptions).DisableRateLimiting();

    app.UseSecurityHeaders();

    app.UseRequestContextLogging();

    app.UseSerilogRequestLogging();

    // REMARK: Keep the exception handler inside the request logging (and the CorrelationId scope):
    // request logs then record the handled status (499 as Information, 500 as Error), each exception
    // is logged once, and the handler's logs carry the CorrelationId.
    app.UseExceptionHandler();

    app.UseCors();

    app.UseAuthentication();

    app.UseAuthorization();

    app.UseRateLimiter();

    // REMARK: If you want to use Controllers, you'll need this.
    app.MapControllers();

    await app.RunAsync();
}
// REMARK: HostAbortedException is thrown on purpose by design-time tools such as `dotnet ef`.
#pragma warning disable S2139 // Top-level handler: log the fatal error, then rethrow so the process exits with a failure code.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");

    // REMARK: Rethrowing also lets test hosts (WebApplicationFactory) see startup errors.
    throw;
}
#pragma warning restore S2139
finally
{
    await Log.CloseAndFlushAsync();
}

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
