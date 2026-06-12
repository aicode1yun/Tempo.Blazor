namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>SMTP delivery options bound from the <c>Smtp</c> configuration section.</summary>
public sealed class SmtpOptions
{
    /// <summary>SMTP host (default localhost for smtp4dev).</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP port (default 2525 for smtp4dev).</summary>
    public int Port { get; set; } = 2525;

    /// <summary>Connection security: <c>None</c>, <c>StartTls</c> or <c>SslOnConnect</c>.</summary>
    public string Security { get; set; } = "None";

    /// <summary>Optional SMTP username (no auth when empty — as with smtp4dev).</summary>
    public string? Username { get; set; }

    /// <summary>Optional SMTP password.</summary>
    public string? Password { get; set; }

    /// <summary>Default sender address used when a message has no explicit From.</summary>
    public string FromAddress { get; set; } = "no-reply@tempo.local";

    /// <summary>Default sender display name.</summary>
    public string FromName { get; set; } = "Tempo";

    /// <summary>Maximum send attempts on transient failures.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay between retries (doubled each attempt). Set to zero in tests.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
