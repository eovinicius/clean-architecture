using Application.Abstractions.Messaging;
using Application.Users.RequestPasswordReset;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class ForgotPassword : IEndpoint
{
    public sealed record Request(string Email);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/forgot-password", async (
            Request request,
            ICommandHandler<RequestPasswordResetCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RequestPasswordResetCommand(request.Email);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(() => Results.Accepted(), CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .WithSummary("Send a password reset token by email")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
