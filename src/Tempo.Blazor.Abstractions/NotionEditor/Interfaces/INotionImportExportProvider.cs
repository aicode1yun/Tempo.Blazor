namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Enums;

public interface INotionImportExportProvider
{
    Task<Stream> ExportPageAsync(string pageId, NotionExportFormat format);
    Task<Stream> ExportPageWithSubpagesAsync(string pageId, NotionExportFormat format);
    Task<INotionPage> ImportAsync(Stream content, NotionImportFormat format, string? targetParentPageId);
}
