namespace Domain.Users;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; }
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
