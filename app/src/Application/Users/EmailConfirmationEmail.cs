using Application.Abstractions.Notifications;

namespace Application.Users;

internal static class EmailConfirmationEmail
{
    // SPEC-0003 RN-2: o assunto é fixo; o token vai só no corpo, porque o assunto pode ser logado.
    private const string Subject = "Confirm your email";

    public static Task SendAsync(IEmailSender emailSender, string recipient, string token) =>
        // The data is already committed: cancelling here would lose the email, so the send is never cancelled.
        emailSender.SendAsync(
            recipient,
            Subject,
            $"Use the token below to confirm your email address. It expires in 24 hours.\n\n{token}",
            CancellationToken.None);
}
