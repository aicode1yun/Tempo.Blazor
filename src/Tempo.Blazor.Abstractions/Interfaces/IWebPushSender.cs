using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>Sends an encrypted Web Push message to a browser subscription (VAPID).</summary>
public interface IWebPushSender
{
    /// <summary>Sends <paramref name="payload"/> to <paramref name="subscription"/>.</summary>
    Task<TmWebPushResult> SendAsync(
        TmPushSubscription subscription,
        TmWebPushPayload payload,
        CancellationToken cancellationToken = default);
}
