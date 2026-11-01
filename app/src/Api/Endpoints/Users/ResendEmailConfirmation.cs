using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.ResendEmailConfirmation;
using SharedKernel;

namespace Api.Endpoints.Users;

internal sealed class ResendEmailConfirmation : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/resend-email-confirmation", async (
            Request request,
            ICommandHandler<ResendEmailConfirmationCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ResendEmailConfirmationCommand(request.Email);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(() => Results.Accepted(), CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithSummary("Send a new email confirmation token")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
