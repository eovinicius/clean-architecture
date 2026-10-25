using System.Collections.Concurrent;
using Application.Abstractions.Notifications;

namespace IntegrationTests;

// Replaces the real sender so tests can read the emails (and the tokens inside them) the API sent.
// Shared by the whole collection, so always filter by the unique recipient of the test.
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _emails = new();

    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        _emails.Enqueue(new SentEmail(recipient, subject, body));

        return Task.CompletedTask;
    }

    public IReadOnlyList<SentEmail> SentTo(string recipient) =>
        [.. _emails.Where(email => string.Equals(email.Recipient, recipient, StringComparison.Ordinal))];

    public sealed record SentEmail(string Recipient, string Subject, string Body)
    {
        // The token is the last line of the body (Application/Users/EmailConfirmationEmail.cs).
        public string Token => Body.Split('\n')[^1];
    }
}
