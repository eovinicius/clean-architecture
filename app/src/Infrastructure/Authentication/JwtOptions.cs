using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 needs a key of at least 256 bits; a shorter secret would only fail when signing.
    [Required, MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpirationInMinutes { get; init; }

    [Range(1, int.MaxValue)]
    public int RefreshTokenExpirationInDays { get; init; }
}
