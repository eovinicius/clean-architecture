using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Users.DeleteExpiredUnconfirmed;

internal sealed class DeleteExpiredUnconfirmedUsersCommandHandler(
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<DeleteExpiredUnconfirmedUsersCommand, int>
{
    public async Task<Result<int>> Handle(DeleteExpiredUnconfirmedUsersCommand command, CancellationToken cancellationToken) =>
        await userRepository.DeleteExpiredUnconfirmedAsync(dateTimeProvider.UtcNow, cancellationToken);
}
