namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>
/// A browser Web Push subscription (the PushSubscription JSON produced by the Push API),
/// associated with a Tempo user so the server can send VAPID pushes.
/// </summary>
public sealed class TmPushSubscription
{
    /// <summary>User this subscription belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Push service endpoint URL (unique per subscription).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Client public key (base64url, the <c>p256dh</c> key).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Client auth secret (base64url, the <c>auth</c> key).</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Optional expiration time reported by the push service.</summary>
    public DateTimeOffset? ExpirationTime { get; set; }

    /// <summary>When the subscription was registered.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Returns true when the required subscription fields are present.</summary>
    public bool IsValid
        => !string.IsNullOrWhiteSpace(UserId)
        && !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(P256dh)
        && !string.IsNullOrWhiteSpace(Auth);
}
