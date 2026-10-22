namespace Domain.Users;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public string TokenHash { get; private set; }
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public static RefreshToken Issue(Guid userId, string tokenHash, Guid familyId, DateTime expiresOnUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            FamilyId = familyId,
            UserId = userId,
            ExpiresOnUtc = expiresOnUtc
        };

    public bool IsExpired(DateTime utcNow) => ExpiresOnUtc < utcNow;

    public void Revoke(DateTime utcNow) => RevokedAtUtc = utcNow;
}
