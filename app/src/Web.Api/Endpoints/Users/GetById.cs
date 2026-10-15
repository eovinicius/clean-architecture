using Application.Abstractions.Messaging;
using Application.Users.GetById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/{userId:guid}", async (
            Guid userId,
            IQueryHandler<GetUserByIdQuery, UserResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUserByIdQuery(userId);

            Result<UserResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.UsersAccess)
        .WithTags(Tags.Users)
        .WithSummary("Get a user by id")
        .Produces<UserResponse>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
