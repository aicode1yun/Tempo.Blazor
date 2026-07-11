using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Abstractions.Interfaces;

/// <summary>
/// Optional hook that records and manages historical versions of a file. When supplied to a
/// file component, re-uploading over an existing item creates a new version; the version history
/// can be listed, compared (diff), and restored. Entirely additive — components work unchanged
/// when no hook is supplied.
/// </summary>
public interface IFileVersioningHook
{
    /// <summary>Records a new version for an item and returns it (marked current).</summary>
    Task<TmFileVersion> CreateVersionAsync(FileVersionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the version history for an item, newest first.</summary>
    Task<IReadOnlyList<TmFileVersion>> GetVersionsAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>Restores a prior version, making a copy of it the new current version.</summary>
    Task<TmFileVersion> RestoreVersionAsync(string itemId, string versionId, CancellationToken cancellationToken = default);

    /// <summary>Computes a diff between two versions of an item (older → newer).</summary>
    Task<TmFileVersionDiff> DiffAsync(string itemId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default);
}
