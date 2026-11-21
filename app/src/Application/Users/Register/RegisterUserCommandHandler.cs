using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        if (await userRepository.EmailExistsAsync(email, cancellationToken))
        {
            return UserErrors.EmailNotUnique;
        }

        var user = User.Create(email, command.FirstName, command.LastName, passwordHasher.Hash(command.Password), dateTimeProvider.UtcNow);

        userRepository.Add(user);

        // The confirmation email goes out from UserRegisteredDomainEventHandler, after the user is persisted.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
