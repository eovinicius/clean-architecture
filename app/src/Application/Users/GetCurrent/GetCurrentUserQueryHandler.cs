using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetCurrent;

internal sealed class GetCurrentUserQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        CurrentUserResponse? user = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => new CurrentUserResponse
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound(userId);
        }

        return user;
    }
}
