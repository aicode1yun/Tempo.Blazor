namespace Tempo.Blazor.Abstractions.Shared;

/// <summary>Optional file provider contract for draft asset workflows.</summary>
public interface ITmDraftFileProvider
{
    /// <summary>Deletes a draft asset that was never committed.</summary>
    /// <param name="entityRef">Entity that owns the draft asset.</param>
    /// <param name="assetId">Draft asset id.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task DeleteDraftAsync(
        TmEntityRef entityRef,
        string assetId,
        CancellationToken cancellationToken = default);

    /// <summary>Commits draft assets that are now referenced by the saved entity.</summary>
    /// <param name="entityRef">Entity that owns the assets.</param>
    /// <param name="assetIds">Asset ids to commit.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task<TmFileCommitResult> CommitDraftsAsync(
        TmEntityRef entityRef,
        IReadOnlyList<string> assetIds,
        CancellationToken cancellationToken = default);
}
