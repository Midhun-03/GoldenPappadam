using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GoldenPappadam.Infrastructure.Email;

/// <summary>Used until a mail provider is configured, so a send fails with a reason instead of vanishing.</summary>
public class UnconfiguredEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct) =>
        throw new EmailDeliveryException(
            "Email is not set up on the server yet. Configure the Email settings, then retry.");
}

/// <summary>
/// Development: writes each message as an .eml file instead of sending it. Open one with Outlook
/// or Thunderbird to see exactly what the customer would receive, attachment included.
/// </summary>
public class PickupDirectoryEmailSender(string directory, EmailOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);

        var mime = MimeMessageFactory.Build(message, options);
        var path = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");

        await mime.WriteToAsync(path, ct);
    }
}

public class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new EmailDeliveryException("Email is set to SMTP but the host or the from-address is missing.");
        }

        var mime = MimeMessageFactory.Build(message, options);
        using var client = new SmtpClient { Timeout = 30_000 };

        try
        {
            await client.ConnectAsync(
                options.Host,
                options.Port,
                options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                ct);

            if (!string.IsNullOrEmpty(options.Username))
            {
                await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, ct);
            }

            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // MailKit's messages name the server and its SMTP reply, which is what an admin needs
            // to see; they never contain the password.
            throw new EmailDeliveryException(exception.Message, exception);
        }
    }
}

internal static class MimeMessageFactory
{
    public static MimeMessage Build(EmailMessage message, EmailOptions options)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromEmail ?? "no-reply@localhost"));
        mime.To.Add(MailboxAddress.Parse(message.To));

        if (message.ReplyTo is not null)
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        }

        mime.Subject = message.Subject;

        var body = new BodyBuilder { TextBody = message.TextBody };

        foreach (var attachment in message.Attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        mime.Body = body.ToMessageBody();

        return mime;
    }
}
