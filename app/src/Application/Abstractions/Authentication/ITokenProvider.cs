using Domain.Users;

namespace Application.Abstractions.Authentication;

public interface ITokenProvider
{
    TimeSpan RefreshTokenLifetime { get; }

    string Create(User user);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
