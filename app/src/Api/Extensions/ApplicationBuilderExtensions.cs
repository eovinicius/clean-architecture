using Scalar.AspNetCore;

namespace Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication MapOpenApiWithScalar(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();

        return app;
    }
}
