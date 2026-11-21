namespace Api.Controllers.Users;

public sealed record RegisterRequest(string Email, string FirstName, string LastName, string Password);
