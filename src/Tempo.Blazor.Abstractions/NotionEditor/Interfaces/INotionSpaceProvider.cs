using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionSpaceProvider
{
    Task<IReadOnlyList<NotionSpaceDto>> GetSpacesAsync(CancellationToken cancellationToken = default);
    Task<NotionSpaceDto?> GetSpaceAsync(string spaceId, CancellationToken cancellationToken = default);
    Task<NotionSpaceDto> CreateSpaceAsync(NotionSpaceDto space, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<INotionPage>> GetPagesInSpaceAsync(string spaceId, CancellationToken cancellationToken = default);
    Task MovePageToSpaceAsync(string pageId, string spaceId, CancellationToken cancellationToken = default);
}
