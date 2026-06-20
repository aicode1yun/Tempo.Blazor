using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

/// <summary>Optional provider for page analytics metadata.</summary>
public interface INotionAnalyticsProvider
{
    /// <summary>Records a page view for the given user or anonymous visitor.</summary>
    Task RecordViewAsync(Guid pageId, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Returns analytics for the requested page, or null when analytics are unavailable.</summary>
    Task<PageAnalyticsDto?> GetPageAnalyticsAsync(Guid pageId, CancellationToken cancellationToken = default);

    /// <summary>Returns the most viewed pages for a space in the requested range.</summary>
    Task<IReadOnlyList<PageAnalyticsDto>> GetTopPagesAsync(string spaceId, NotionAnalyticsRange range, CancellationToken cancellationToken = default);
}
