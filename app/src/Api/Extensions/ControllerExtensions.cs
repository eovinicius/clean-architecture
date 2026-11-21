using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Api.Extensions;

internal static class ControllerExtensions
{
    private const string TextJson = "text/json";
    private const string AnyStructuredJson = "application/*+json";

    internal static IServiceCollection AddControllersWithJsonContract(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            // Non-nullable request properties are not implicitly [Required]: missing fields reach the
            // FluentValidation validators, which answer with the same validation problem as every other rule.
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

            // The API speaks application/json only (requests and documented responses), so the contract
            // does not advertise text/json, text/plain or application/*+json. The output formatter keeps
            // its application/*+json wildcard, needed to write application/problem+json results.
            options.OutputFormatters.RemoveType<StringOutputFormatter>();

            foreach (SystemTextJsonInputFormatter input in options.InputFormatters.OfType<SystemTextJsonInputFormatter>())
            {
                input.SupportedMediaTypes.Remove(TextJson);
                input.SupportedMediaTypes.Remove(AnyStructuredJson);
            }

            foreach (SystemTextJsonOutputFormatter output in options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>())
            {
                output.SupportedMediaTypes.Remove(TextJson);
            }
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            // Binding failures (malformed JSON, missing required query value) answer a plain 400 ProblemDetails:
            // the deserializer's message (path, byte position) never reaches the client. Business validation
            // still answers through the handlers' Validation.General problem.
            options.InvalidModelStateResponseFactory = _ => new BadRequestResult();
        });

        return services;
    }
}
