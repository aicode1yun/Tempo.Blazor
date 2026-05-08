namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Audit event captured during a signing ceremony.</summary>
public sealed class SigningAuditTrailEvent
{
    /// <summary>Stable event identifier.</summary>
    public string Uuid { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable event label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Actor display name or system label.</summary>
    public string? Actor { get; init; }

    /// <summary>Time when the event occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>IP address observed for the event.</summary>
    public string? IpAddress { get; init; }

    /// <summary>User agent observed for the event.</summary>
    public string? UserAgent { get; init; }

    /// <summary>Timezone observed for the event.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Verification method associated with the event.</summary>
    public string? VerificationMethod { get; init; }
}
