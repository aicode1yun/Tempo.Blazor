namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionSearchProvider
{
    Task<IEnumerable<INotionPage>> SearchPagesAsync(string query, NotionSearchFilter? filter);
    Task<IEnumerable<NotionSearchResult>> SearchBlocksAsync(string query, NotionSearchFilter? filter);
    Task<(IEnumerable<INotionPage> Pages, IEnumerable<NotionSearchResult> Blocks)> SearchAllAsync(string query, NotionSearchFilter? filter, int maxResults);
}
