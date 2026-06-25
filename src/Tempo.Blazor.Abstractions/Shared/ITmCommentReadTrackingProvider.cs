namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Optional comment provider contract for read tracking.</summary>
public interface ITmCommentReadTrackingProvider
{
    /// <summary>Marks a thread as read for a user.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task MarkThreadAsReadAsync(
        string threadId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks a thread as unread for a user.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task MarkThreadAsUnreadAsync(
        string threadId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks all threads for an entity as read for a user.</summary>
    /// <param name="entityRef">Target entity.</param>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task MarkAllForEntityAsReadAsync(
        TmEntityRef entityRef,
        string userId,
        CancellationToken cancellationToken = default);
}
