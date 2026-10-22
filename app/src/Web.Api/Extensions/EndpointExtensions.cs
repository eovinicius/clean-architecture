using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Web.Api.Endpoints;
using Web.Api.Infrastructure;

namespace Web.Api.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] serviceDescriptors = [.. assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))];

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(
        this WebApplication app,
        RouteGroupBuilder? routeGroupBuilder = null)
    {
        IEnumerable<IEndpoint> endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;

        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }

    public static RouteHandlerBuilder HasPermission(this RouteHandlerBuilder builder, string permission)
    {
        return builder.RequireAuthorization(permission);
    }

    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
    {
        return builder
            .AddEndpointFilter<IdempotencyFilter>()
            .AddOpenApiOperationTransformer(async (operation, context, cancellationToken) =>
            {
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
                            ["application/problem+json"] = new() { Schema = problemSchema }
                        }
                    });
            });
    }
}
