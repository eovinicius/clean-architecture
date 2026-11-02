namespace Domain.Users;

// A single-use token sent by email; only its hash is persisted.
public sealed class UserToken
{
    private static readonly TimeSpan ReissueInterval = TimeSpan.FromSeconds(60);

    private UserToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public UserTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    public static UserToken Issue(Guid userId, UserTokenPurpose purpose, string tokenHash, DateTime utcNow) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            CreatedAtUtc = utcNow,
            ExpiresOnUtc = utcNow.Add(LifetimeOf(purpose))
        };

    public bool IsExpired(DateTime utcNow) => utcNow > ExpiresOnUtc;

    public bool IsUsable(DateTime utcNow) => UsedAtUtc is null && !IsExpired(utcNow);

    public bool AllowsReissue(DateTime utcNow) => utcNow - CreatedAtUtc > ReissueInterval;

    public void MarkUsed(DateTime utcNow) => UsedAtUtc = utcNow;

    private static TimeSpan LifetimeOf(UserTokenPurpose purpose) => purpose switch
    {
        UserTokenPurpose.EmailConfirmation => TimeSpan.FromHours(24),
        UserTokenPurpose.PasswordReset => TimeSpan.FromHours(1),
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown user token purpose.")
    };
}
