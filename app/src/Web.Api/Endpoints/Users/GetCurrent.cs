using Application.Abstractions.Messaging;
using Application.Users.GetCurrent;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class GetCurrent : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me", async (
            IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Users)
        .WithSummary("Get the current user")
        .Produces<CurrentUserResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
