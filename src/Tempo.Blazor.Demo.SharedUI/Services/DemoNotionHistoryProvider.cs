using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Services;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionHistoryProvider : INotionVersionProvider
{
    private readonly HttpClient _http;

    public DemoNotionHistoryProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<PagedResult<IPageVersion>> GetVersionsAsync(string pageId, int page, int pageSize)
    {
        var result = await _http.GetFromJsonAsync<PagedResult<NotionPageVersionDto>>(
            $"/api/notion/history/pages/{pageId}/versions?page={page}&pageSize={pageSize}");

        return new PagedResult<IPageVersion>
        {
            Items = result?.Items.Cast<IPageVersion>().ToList() ?? [],
            TotalCount = result?.TotalCount ?? 0,
            Page = result?.Page ?? page,
            PageSize = result?.PageSize ?? pageSize
        };
    }

    public async Task<IPageVersion> GetVersionAsync(string pageId, string versionId)
    {
        var version = await _http.GetFromJsonAsync<NotionPageVersionDto>(
            $"/api/notion/history/pages/{pageId}/versions/{versionId}");
        return version ?? throw new KeyNotFoundException($"Notion page version {versionId} was not found.");
    }

    public async Task RestoreVersionAsync(string pageId, string versionId)
    {
        using var response = await _http.PostAsync($"/api/notion/history/pages/{pageId}/versions/{versionId}/restore", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<BlockDiff>> GetDiffAsync(string pageId, string versionIdA, string versionIdB)
    {
        var result = await _http.GetFromJsonAsync<IReadOnlyList<BlockDiff>>(
            $"/api/notion/history/pages/{pageId}/diff?fromVersionId={Uri.EscapeDataString(versionIdA)}&toVersionId={Uri.EscapeDataString(versionIdB)}");

        return result ?? [];
    }

    public async Task<IEnumerable<BlockDiff>> CompareVersionsAsync(string versionId1, string versionId2)
    {
        var before = await GetVersionByIdAsync(versionId1);
        return await GetDiffAsync(before.PageId.ToString("D"), versionId1, versionId2);
    }

    private async Task<NotionPageVersionDto> GetVersionByIdAsync(string versionId)
    {
        var version = await _http.GetFromJsonAsync<NotionPageVersionDto>($"/api/notion/history/versions/{versionId}");
        return version ?? throw new KeyNotFoundException($"Notion page version {versionId} was not found.");
    }

}
