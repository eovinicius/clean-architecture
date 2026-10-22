using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity
{
    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string PasswordHash { get; private set; }

    public static User Create(string email, string firstName, string lastName, string passwordHash)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email));

        return user;
    }
}
