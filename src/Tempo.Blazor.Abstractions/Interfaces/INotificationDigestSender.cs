using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>Delivers a generated notification digest to a recipient (typically by email).</summary>
public interface INotificationDigestSender
{
    /// <summary>Sends the digest. Implementations decide the transport (SMTP, webhook, etc.).</summary>
    Task SendAsync(TmNotificationDigest digest, CancellationToken cancellationToken = default);
}
