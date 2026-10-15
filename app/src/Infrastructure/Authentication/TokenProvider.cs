using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Authentication;
using Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharedKernel;

namespace Infrastructure.Authentication;

internal sealed class TokenProvider(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider) : ITokenProvider
{
    private readonly JwtOptions _jwt = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_jwt.RefreshTokenExpirationInDays);

    public string Create(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            ]),
            Expires = dateTimeProvider.UtcNow.AddMinutes(_jwt.ExpirationInMinutes),
            SigningCredentials = credentials,
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience
        };

        var handler = new JsonWebTokenHandler();

        string token = handler.CreateToken(tokenDescriptor);

        return token;
    }

    public string GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(randomBytes);
    }

    // Refresh tokens carry 256 bits of entropy, so an unsalted SHA-256 is enough and keeps lookups indexable.
    public string HashRefreshToken(string refreshToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToHexString(hash);
    }
}
