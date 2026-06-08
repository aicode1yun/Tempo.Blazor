using System.Net.Http.Json;
using System.Net.Http.Headers;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed Notion import/export provider used by the demo applications.</summary>
public sealed class DemoNotionImportExportProvider : INotionImportExportProvider
{
    private readonly HttpClient _http;

    public DemoNotionImportExportProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<Stream> ExportPageAsync(string pageId, NotionExportFormat format)
    {
        var bytes = await ExportBytesAsync(pageId, format, includeSubpages: false);
        return new MemoryStream(bytes, writable: false);
    }

    public async Task<Stream> ExportPageWithSubpagesAsync(string pageId, NotionExportFormat format)
    {
        var bytes = await ExportBytesAsync(pageId, format, includeSubpages: true);
        return new MemoryStream(bytes, writable: false);
    }

    public async Task<INotionPage> ImportAsync(Stream content, NotionImportFormat format, string? targetParentPageId)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(format.ToString()), "format");
        if (!string.IsNullOrWhiteSpace(targetParentPageId))
        {
            form.Add(new StringContent(targetParentPageId), "targetParentPageId");
        }

        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentTypeFor(format));
        form.Add(fileContent, "file", FileNameFor(format));

        using var response = await _http.PostAsync("/api/notion/pages/import", form);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<NotionPage>()
            ?? throw new InvalidOperationException("Imported page response was empty.");
    }

    private async Task<byte[]> ExportBytesAsync(string pageId, NotionExportFormat format, bool includeSubpages)
    {
        using var response = await _http.GetAsync($"/api/notion/pages/{pageId}/export/{format}?includeSubpages={includeSubpages}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string FileNameFor(NotionImportFormat format) => format switch
    {
        NotionImportFormat.Word => "notion-import.docx",
        NotionImportFormat.Html => "notion-import.html",
        NotionImportFormat.Markdown => "notion-import.md",
        _ => "notion-import"
    };

    private static string ContentTypeFor(NotionImportFormat format) => format switch
    {
        NotionImportFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        NotionImportFormat.Html => "text/html; charset=utf-8",
        NotionImportFormat.Markdown => "text/markdown; charset=utf-8",
        _ => "application/octet-stream"
    };

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(string.IsNullOrWhiteSpace(message)
            ? $"Notion import/export request failed with status {(int)response.StatusCode}."
            : message);
    }
}
