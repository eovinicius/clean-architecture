using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Web.Api.Extensions;

// Enums travel as numbers (no JsonStringEnumConverter), and the generated schema is then a bare
// "integer". List the allowed values and their names so clients can see what each number means.
internal sealed class NumericEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

        if (!type.IsEnum || schema.Enum is { Count: > 0 })
        {
            return Task.CompletedTask;
        }

        (long Value, string Name)[] members = [.. Enum.GetValues(type)
            .Cast<object>()
            .Select(value => (Convert.ToInt64(value, CultureInfo.InvariantCulture), Enum.GetName(type, value)!))];

        schema.Enum = [.. members.Select(member => (JsonNode)JsonValue.Create(member.Value))];
        schema.Description ??= string.Join(", ", members.Select(member => $"{member.Value} = {member.Name}"));

        return Task.CompletedTask;
    }
}
