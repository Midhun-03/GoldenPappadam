namespace GoldenPappadam.Infrastructure.Email;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    IReadOnlyList<EmailAttachment> Attachments,
    string? ReplyTo = null);

/// <summary>
/// Sends mail. Throws <see cref="EmailDeliveryException"/> when the message did not go, with a
/// message safe to show an admin - never a password or a stack trace.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public class EmailDeliveryException(string message, Exception? inner = null) : Exception(message, inner);
