namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionHistoryProvider
{
    Task<PagedResult<IPageVersion>> GetVersionsAsync(string pageId, int page, int pageSize);
    Task<IPageVersion> GetVersionAsync(string pageId, string versionId);
    Task RestoreVersionAsync(string pageId, string versionId);
    Task<IEnumerable<BlockDiff>> CompareVersionsAsync(string versionId1, string versionId2);
}
