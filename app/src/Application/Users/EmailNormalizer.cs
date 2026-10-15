using System.Diagnostics.CodeAnalysis;

namespace Application.Users;

internal static class EmailNormalizer
{
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Emails are stored and returned in lowercase; this is canonicalization, not a comparison.")]
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
