using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error Unauthorized() => Error.Forbidden(
        "Users.Unauthorized",
        "You are not authorized to perform this action.");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Users.InvalidCredentials",
        "The provided credentials are invalid");

    public static readonly Error InvalidRefreshToken = Error.Problem(
        "Users.InvalidRefreshToken",
        "The provided refresh token is invalid or has expired");

    public static readonly Error InvalidEmailConfirmationToken = Error.Problem(
        "Users.InvalidEmailConfirmationToken",
        "The provided email confirmation token is invalid or has expired");

    public static readonly Error InvalidPasswordResetToken = Error.Problem(
        "Users.InvalidPasswordResetToken",
        "The provided password reset token is invalid or has expired");

    public static readonly Error EmailNotConfirmed = Error.Forbidden(
        "Users.EmailNotConfirmed",
        "The email address has not been confirmed yet");
}
