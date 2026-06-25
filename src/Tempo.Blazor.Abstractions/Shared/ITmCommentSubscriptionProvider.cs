namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Optional comment provider contract for thread subscriptions.</summary>
public interface ITmCommentSubscriptionProvider
{
    /// <summary>Subscribes a user to a thread.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task SubscribeAsync(
        string threadId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Unsubscribes a user from a thread.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task UnsubscribeAsync(
        string threadId,
        string userId,
        CancellationToken cancellationToken = default);
}
