namespace Web.Api.Extensions;

internal static class CorsExtensions
{
    internal static IServiceCollection AddCorsInternal(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy
                .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }
}
