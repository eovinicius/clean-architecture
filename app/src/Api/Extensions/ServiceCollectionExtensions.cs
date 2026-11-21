using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;

namespace Api.Extensions;

internal static class ServiceCollectionExtensions
{
    private const string RequestNamespacePrefix = "Api.Controllers.";

    internal static IServiceCollection AddOpenApiWithAuth(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.CreateSchemaReferenceId = CreateSchemaReferenceId;
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<SecurityResponsesOperationTransformer>();
            options.AddOperationTransformer<IdempotencyOperationTransformer>();
            options.AddSchemaTransformer<NumericEnumSchemaTransformer>();
        });

        return services;
    }

    // Request types live in Api/Controllers/{Feature}/ and are named after the use case
    // (Controllers/Users/RegisterRequest), so the bare type name alone could collide across features.
    // Prefixing the feature keeps every schema distinct: "UsersRegisterRequest".
    private static string? CreateSchemaReferenceId(JsonTypeInfo jsonTypeInfo)
    {
        Type type = jsonTypeInfo.Type;

        if (type.Namespace is not { } ns || !ns.StartsWith(RequestNamespacePrefix, StringComparison.Ordinal))
        {
            return OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        }

        return $"{ns[RequestNamespacePrefix.Length..]}{type.Name}";
    }
}
