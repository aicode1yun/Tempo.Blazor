using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Optional provider for Notion page versions, restore, and version diffs.</summary>
public interface INotionVersionProvider
{
    /// <summary>Gets a paged list of versions for a page.</summary>
    Task<PagedResult<IPageVersion>> GetVersionsAsync(string pageId, int page, int pageSize);

    /// <summary>Gets a single version snapshot.</summary>
    Task<IPageVersion> GetVersionAsync(string pageId, string versionId);

    /// <summary>Restores a page to a historical version.</summary>
    Task RestoreVersionAsync(string pageId, string versionId);

    /// <summary>Compares two version snapshots.</summary>
    Task<IEnumerable<BlockDiff>> CompareVersionsAsync(string versionId1, string versionId2);
}
