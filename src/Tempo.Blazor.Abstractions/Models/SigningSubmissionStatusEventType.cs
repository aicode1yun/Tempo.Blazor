namespace Tempo.Blazor.Abstractions.Models;

/// <summary>Known submission lifecycle event types.</summary>
public enum SigningSubmissionStatusEventType
{
    /// <summary>The submission was sent to a recipient.</summary>
    Sent,

    /// <summary>The recipient opened the signing link.</summary>
    Opened,

    /// <summary>The recipient completed signing.</summary>
    Completed,

    /// <summary>The recipient declined signing.</summary>
    Declined,

    /// <summary>An email bounced.</summary>
    EmailBounced,

    /// <summary>An email complaint was recorded.</summary>
    EmailComplaint,

    /// <summary>An identity verification was completed.</summary>
    VerificationCompleted,

    /// <summary>A knowledge-based authentication step was completed.</summary>
    KbaCompleted
}
