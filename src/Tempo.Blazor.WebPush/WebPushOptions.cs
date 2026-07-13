namespace Tempo.Blazor.WebPush;

/// <summary>VAPID configuration for Web Push. Bound from the <c>WebPush</c> configuration section;
/// when the keys are empty a host may generate an ephemeral pair at startup.</summary>
public sealed class WebPushOptions
{
    /// <summary>VAPID subject (a <c>mailto:</c> or origin URL).</summary>
    public string Subject { get; set; } = "mailto:no-reply@tempo.local";

    /// <summary>VAPID public key (base64url, uncompressed P-256 point).</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>VAPID private key (base64url).</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>True when a usable VAPID key pair is configured.</summary>
    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
