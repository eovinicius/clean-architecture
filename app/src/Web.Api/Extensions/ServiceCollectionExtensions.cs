using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;

namespace Web.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.CreateSchemaReferenceId = CreateSchemaReferenceId;
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<SecurityResponsesOperationTransformer>();
            options.AddSchemaTransformer<NumericEnumSchemaTransformer>();
        });

        return services;
    }

    // Endpoint request types are nested (Register.Request, Login.Request, ...) and the default
    // id is the bare type name, so they would all collide into one "Request" schema.
    // Prefixing the feature and use case keeps every schema distinct: "UsersRegisterRequest".
    private static string? CreateSchemaReferenceId(JsonTypeInfo jsonTypeInfo)
    {
        Type type = jsonTypeInfo.Type;

        if (type is not { IsNested: true, DeclaringType: { } declaringType })
        {
            return OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        }

        string feature = declaringType.Namespace?.Split('.')[^1] ?? string.Empty;

        return $"{feature}{declaringType.Name}{type.Name}";
    }

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
