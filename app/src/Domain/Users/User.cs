using System.Linq.Expressions;
using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity
{
    // SPEC-0003 RN-16: quem não confirma o e-mail nesse prazo é removido e libera o e-mail.
    private static readonly TimeSpan UnconfirmedRegistrationLifetime = TimeSpan.FromDays(7);

    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public DateTime? EmailConfirmedAt { get; private set; }

    public bool IsEmailConfirmed => EmailConfirmedAt is not null;

    public static User Create(string email, string firstName, string lastName, string passwordHash, DateTime registeredAt)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash,
            RegisteredAt = registeredAt
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email));

        return user;
    }

    // An expression, not a method, so the cleanup can run as a single DELETE in the database.
    public static Expression<Func<User, bool>> IsUnconfirmedRegistrationExpired(DateTime utcNow)
    {
        DateTime registeredBefore = utcNow - UnconfirmedRegistrationLifetime;

        return user => user.EmailConfirmedAt == null && user.RegisteredAt < registeredBefore;
    }

    public void ConfirmEmail(DateTime confirmedAt)
    {
        // SPEC-0003 RN-10: confirmar de novo não altera o instante original.
        if (IsEmailConfirmed)
        {
            return;
        }

        EmailConfirmedAt = confirmedAt;

        Raise(new UserEmailConfirmedDomainEvent(Id));
    }
}
