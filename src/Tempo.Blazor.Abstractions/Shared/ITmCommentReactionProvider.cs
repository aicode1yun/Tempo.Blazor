namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Optional comment provider contract for entry reactions.</summary>
public interface ITmCommentReactionProvider
{
    /// <summary>Gets reactions for a comment entry.</summary>
    /// <param name="entryId">Entry id to inspect.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<IReadOnlyList<TmCommentReaction>> GetReactionsAsync(
        string entryId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a reaction to a comment entry.</summary>
    /// <param name="entryId">Target entry id.</param>
    /// <param name="value">Emoji or compact reaction value.</param>
    /// <param name="userId">User adding the reaction.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task AddReactionAsync(
        string entryId,
        string value,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a reaction from a comment entry.</summary>
    /// <param name="entryId">Target entry id.</param>
    /// <param name="value">Emoji or compact reaction value.</param>
    /// <param name="userId">User removing the reaction.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task RemoveReactionAsync(
        string entryId,
        string value,
        string userId,
        CancellationToken cancellationToken = default);
}
