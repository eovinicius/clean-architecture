using Application.Abstractions.Notifications;

namespace Application.Users;

internal static class PasswordResetEmail
{
    // SPEC-0004 RN-2: o assunto é fixo; o token vai só no corpo, porque o assunto pode ser logado.
    private const string Subject = "Reset your password";

    public static Task SendAsync(IEmailSender emailSender, string recipient, string token) =>
        // The data is already committed: cancelling here would lose the email, so the send is never cancelled.
        emailSender.SendAsync(
            recipient,
            Subject,
            $"Use the token below to choose a new password. It expires in 1 hour. If you did not ask for it, ignore this email.\n\n{token}",
            CancellationToken.None);
}
