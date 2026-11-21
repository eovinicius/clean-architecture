namespace Api.Controllers.Users;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
