using Application.Abstractions.Messaging;
using Application.Users.ConfirmEmail;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class ConfirmEmail : IEndpoint
{
    public sealed record Request(string Token);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/confirm-email", async (
            Request request,
            ICommandHandler<ConfirmEmailCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ConfirmEmailCommand(request.Token);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithSummary("Confirm the email address with the token sent by email")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
