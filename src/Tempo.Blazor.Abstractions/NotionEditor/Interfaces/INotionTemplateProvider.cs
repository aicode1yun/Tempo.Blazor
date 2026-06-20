namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Models;

public interface INotionTemplateProvider
{
    Task<IReadOnlyList<NotionTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<NotionTemplateDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
