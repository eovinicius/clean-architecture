using System.Buffers.Text;
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

    // Both are thread-safe and depend only on the validated options, so the singleton builds them once.
    private readonly SigningCredentials _signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Secret)),
        SecurityAlgorithms.HmacSha256);

    private readonly JsonWebTokenHandler _tokenHandler = new();

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_jwt.RefreshTokenExpirationInDays);

    public string Create(User user)
    {
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            ]),
            Expires = dateTimeProvider.UtcNow.AddMinutes(_jwt.ExpirationInMinutes),
            SigningCredentials = _signingCredentials,
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience
        };

        return _tokenHandler.CreateToken(tokenDescriptor);
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

    // SPEC-0003 RN-1: 32 random bytes in base64url (43 chars), safe to put in a link later.
    public string GenerateUserToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    // Same reasoning as refresh tokens: 256 bits of entropy make an unsalted SHA-256 enough.
    public string HashUserToken(string userToken) => HashRefreshToken(userToken);
}
