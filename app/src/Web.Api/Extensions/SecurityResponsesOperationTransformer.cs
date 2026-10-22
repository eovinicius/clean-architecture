using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace Web.Api.Extensions;

// Documents the responses produced by middleware rather than by handlers:
// protected endpoints require the bearer scheme and may answer 401 (and 403 when a
// permission policy applies); the global rate limiter may answer 429 anywhere it is enabled.
internal sealed class SecurityResponsesOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        IAuthorizeData[] authorizeData = [.. metadata.OfType<IAuthorizeData>()];

        if (authorizeData.Length > 0 && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, context.Document)] = []
            });

            AddResponse(operation, StatusCodes.Status401Unauthorized, "Missing or invalid access token.");

            if (authorizeData.Any(data => !string.IsNullOrEmpty(data.Policy)))
            {
                AddResponse(operation, StatusCodes.Status403Forbidden, "The user lacks the required permission.");
            }
        }

        if (!metadata.OfType<DisableRateLimitingAttribute>().Any())
        {
            AddResponse(operation, StatusCodes.Status429TooManyRequests, "Rate limit exceeded; retry later.");
        }

        return Task.CompletedTask;
    }

    private static void AddResponse(OpenApiOperation operation, int statusCode, string description)
    {
        operation.Responses ??= [];
        operation.Responses.TryAdd(
            statusCode.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse { Description = description });
    }
}
