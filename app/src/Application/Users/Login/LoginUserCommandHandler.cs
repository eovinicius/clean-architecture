using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        string email = EmailNormalizer.Normalize(command.Email);

        User? user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            // Spend the same CPU time as a real verification so response timing does not reveal registered emails.
            _ = passwordHasher.Hash(command.Password);

            return UserErrors.InvalidCredentials;
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return UserErrors.InvalidCredentials;
        }

        if (!user.IsEmailConfirmed)
        {
            return UserErrors.EmailNotConfirmed;
        }

        DateTime now = dateTimeProvider.UtcNow;

        IReadOnlyList<RefreshToken> expiredTokens =
            await refreshTokenRepository.GetExpiredByUserAsync(user.Id, now, cancellationToken);

        refreshTokenRepository.RemoveRange(expiredTokens);

        string accessToken = tokenProvider.Create(user);

        (RefreshToken refreshToken, string token) =
            RefreshTokenIssuer.Issue(tokenProvider, user.Id, familyId: Guid.NewGuid(), now);

        refreshTokenRepository.Add(refreshToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, token);
    }
}
