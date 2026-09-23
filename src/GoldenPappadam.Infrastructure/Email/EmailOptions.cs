namespace GoldenPappadam.Infrastructure.Email;

/// <summary>
/// Bound from the "Email" configuration section. The password never belongs in appsettings.json:
/// set it with user-secrets in development and an environment variable (Email__Password) on a server.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>None (sending fails, clearly), Pickup (writes .eml files, for development) or Smtp.</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.None;

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>STARTTLS on 587, the usual choice. Set false for port 465 (implicit TLS) or a local relay.</summary>
    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? FromEmail { get; set; }

    public string FromName { get; set; } = "Golden Pappadam";

    /// <summary>Pickup only: the folder the .eml files are written to, relative to the app.</summary>
    public string PickupDirectory { get; set; } = "App_Data/mail-outbox";
}

public enum EmailProvider
{
    None,
    Pickup,
    Smtp
}
