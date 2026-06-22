namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Minimal provider contract for shared comment threads.</summary>
public interface ITmCommentProvider : ITmCapabilityProvider<TmCommentProviderCapabilities>
{
    /// <summary>Operations this provider supports.</summary>
    new TmCommentProviderCapabilities Capabilities { get; }

    /// <summary>Gets comment threads for an entity.</summary>
    /// <param name="entityRef">Entity to load comments for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmCommentThread>> GetForEntityAsync(
        TmEntityRef entityRef,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a comment thread.</summary>
    /// <param name="thread">Thread to create.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmCommentThread> CreateThreadAsync(
        TmCommentThread thread,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a reply entry to an existing thread.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="entry">Entry to append.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmCommentEntry> ReplyAsync(
        string threadId,
        TmCommentEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing entry.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="entryId">Target entry id.</param>
    /// <param name="entry">Updated entry data.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmCommentEntry> UpdateEntryAsync(
        string threadId,
        string entryId,
        TmCommentEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a comment thread.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task DeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a comment entry.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="entryId">Target entry id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task DeleteEntryAsync(
        string threadId,
        string entryId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a thread.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="resolvedBy">User that resolved the thread, when known.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmCommentThread> ResolveAsync(
        string threadId,
        TmUserRef? resolvedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reopens a resolved thread.</summary>
    /// <param name="threadId">Target thread id.</param>
    /// <param name="reopenedBy">User that reopened the thread, when known.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmCommentThread> ReopenAsync(
        string threadId,
        TmUserRef? reopenedBy = null,
        CancellationToken cancellationToken = default);
}
