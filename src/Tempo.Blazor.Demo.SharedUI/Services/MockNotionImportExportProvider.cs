using System.Net.Http.Json;
using System.Text;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Demo import/export provider. Export returns a minimal in-memory stream;
/// import creates a new page via the API.
/// </summary>
public class MockNotionImportExportProvider : INotionImportExportProvider
{
    private readonly HttpClient _http;

    public MockNotionImportExportProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public Task<Stream> ExportPageAsync(string pageId, NotionExportFormat format)
    {
        var content = format switch
        {
            NotionExportFormat.Html     => "<html><body><h1>Exported Page</h1></body></html>",
            NotionExportFormat.Markdown => "# Exported Page\n\nThis is a demo export.",
            _                           => "%PDF-1.4 demo"
        };
        Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return Task.FromResult(stream);
    }

    public Task<Stream> ExportPageWithSubpagesAsync(string pageId, NotionExportFormat format)
        => ExportPageAsync(pageId, format);

    public async Task<INotionPage> ImportAsync(Stream content, NotionImportFormat format, string? targetParentPageId)
    {
        var page = await _http.PostAsJsonAsync("/api/notion/pages", new { Title = "Imported Page", ParentId = targetParentPageId });
        page.EnsureSuccessStatusCode();
        return await page.Content.ReadFromJsonAsync<NotionPage>() ?? new NotionPage { Title = "Imported Page" };
    }
}
