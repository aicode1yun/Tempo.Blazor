namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionDataProvider
{
    Task<INotionPage> GetPageAsync(string pageId);
    Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId);
    Task<IEnumerable<INotionPage>> GetFavoritesAsync();
    Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count);
    Task<IEnumerable<INotionPage>> GetTrashAsync();
    Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default);
    Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default);
    Task<INotionPage> CreatePageAsync(string? parentId, string title);
    Task UpdatePageAsync(INotionPage page);
    Task DeletePageAsync(string pageId);
    Task RestorePageAsync(string pageId);
    Task PermanentlyDeletePageAsync(string pageId);
    Task ToggleFavoriteAsync(string pageId, bool isFavorite);
    Task MovePageAsync(string pageId, string? newParentId);
    Task<INotionPage> DuplicatePageAsync(string pageId);
}
