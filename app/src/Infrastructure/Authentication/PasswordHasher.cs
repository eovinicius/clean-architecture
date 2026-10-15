using System.Globalization;
using System.Security.Cryptography;
using Application.Abstractions.Authentication;

namespace Infrastructure.Authentication;

internal sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 500000;
    private const string Version = "v1";

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    // Format: v1$<iterations>$<hash hex>$<salt hex>. The legacy format <hash hex>-<salt hex> is still verified.
    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Version}${Iterations}${Convert.ToHexString(hash)}${Convert.ToHexString(salt)}");
    }

    public bool Verify(string password, string passwordHash)
    {
        if (!TryParse(passwordHash, out int iterations, out byte[] hash, out byte[] salt))
        {
            return false;
        }

        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, hash.Length);

        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    }

    private static bool TryParse(string passwordHash, out int iterations, out byte[] hash, out byte[] salt)
    {
        iterations = 0;
        hash = [];
        salt = [];

        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        string[] parts = passwordHash.Split('$');

        if (parts.Length == 4 && parts[0] == Version)
        {
            return int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations) &&
                   iterations > 0 &&
                   TryFromHex(parts[2], out hash) &&
                   TryFromHex(parts[3], out salt);
        }

        string[] legacyParts = passwordHash.Split('-');

        if (parts.Length == 1 && legacyParts.Length == 2)
        {
            iterations = Iterations;

            return TryFromHex(legacyParts[0], out hash) && TryFromHex(legacyParts[1], out salt);
        }

        return false;
    }

    private static bool TryFromHex(string value, out byte[] bytes)
    {
        bytes = [];

        if (value.Length == 0 || value.Length % 2 != 0)
        {
            return false;
        }

        byte[] buffer = new byte[value.Length / 2];

        if (Convert.FromHexString(value, buffer, out _, out _) != System.Buffers.OperationStatus.Done)
        {
            return false;
        }

        bytes = buffer;

        return true;
    }
}
