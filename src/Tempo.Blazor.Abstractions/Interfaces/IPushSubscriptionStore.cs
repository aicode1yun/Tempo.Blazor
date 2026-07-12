using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>Stores browser Web Push subscriptions per user for VAPID delivery.</summary>
public interface IPushSubscriptionStore
{
    /// <summary>Saves (upserts by endpoint) a subscription.</summary>
    Task SaveAsync(TmPushSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>Removes a subscription by its endpoint.</summary>
    Task RemoveAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Returns all subscriptions for a user.</summary>
    Task<IReadOnlyList<TmPushSubscription>> GetForUserAsync(string userId, CancellationToken cancellationToken = default);
}
