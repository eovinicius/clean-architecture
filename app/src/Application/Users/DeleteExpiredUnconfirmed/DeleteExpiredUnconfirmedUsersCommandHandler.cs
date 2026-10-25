using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.DeleteExpiredUnconfirmed;

internal sealed class DeleteExpiredUnconfirmedUsersCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<DeleteExpiredUnconfirmedUsersCommand, int>
{
    // A single DELETE evaluates the criteria in the database, so a confirmation that lands meanwhile is not lost;
    // the user's tokens go with it through the cascading foreign key.
    public async Task<Result<int>> Handle(DeleteExpiredUnconfirmedUsersCommand command, CancellationToken cancellationToken) =>
        await context.Users
            .Where(User.IsUnconfirmedRegistrationExpired(dateTimeProvider.UtcNow))
            .ExecuteDeleteAsync(cancellationToken);
}
