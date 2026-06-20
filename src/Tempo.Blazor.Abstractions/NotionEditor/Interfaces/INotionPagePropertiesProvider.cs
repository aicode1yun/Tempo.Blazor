using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionPagePropertiesProvider
{
    Task<IReadOnlyList<PagePropertiesReportRow>> QueryPagePropertiesAsync(
        PagePropertiesReportQuery query,
        CancellationToken cancellationToken = default);
}
