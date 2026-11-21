using System.Globalization;
using System.Net.Mime;
using Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Extensions;

// Documents the Idempotency-Key header and the 409 answered by IdempotencyFilter on [Idempotent] actions.
internal sealed class IdempotencyOperationTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any())
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyFilter.IdempotencyKeyHeader,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional client-generated key; a repeat with the same key is rejected with 409.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });

        OpenApiSchema problemSchema = await context.GetOrCreateSchemaAsync(
            typeof(ProblemDetails),
            cancellationToken: cancellationToken);

        operation.Responses ??= [];
        operation.Responses.TryAdd(
            StatusCodes.Status409Conflict.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse
            {
                Description = "A request with this idempotency key was already processed.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [MediaTypeNames.Application.ProblemJson] = new() { Schema = problemSchema }
                }
            });
    }
}
