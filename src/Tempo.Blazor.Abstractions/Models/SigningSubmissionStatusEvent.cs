namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Submission status event displayed in a signing timeline.</summary>
public sealed class SigningSubmissionStatusEvent
{
    /// <summary>Stable event identifier.</summary>
    public string Uuid { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Lifecycle event type.</summary>
    public SigningSubmissionStatusEventType Type { get; init; }

    /// <summary>Recipient or actor display name.</summary>
    public string? RecipientName { get; init; }

    /// <summary>Recipient or actor email.</summary>
    public string? RecipientEmail { get; init; }

    /// <summary>When the event occurred.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Additional provider-specific metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
