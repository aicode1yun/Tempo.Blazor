using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionSyncedBlockProvider : INotionSyncedBlockProvider
{
    private readonly HttpClient _http;

    public DemoNotionSyncedBlockProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IEnumerable<IPageBlock>> GetSyncedChildBlocksAsync(string syncId)
    {
        var blocks = await _http.GetFromJsonAsync<List<PageBlock>>($"/api/notion/synced-blocks/{syncId}/children");
        return blocks ?? [];
    }

    public async Task UpdateSyncedChildBlocksAsync(string syncId, IEnumerable<IPageBlock> children)
    {
        var payload = children.OfType<PageBlock>().ToList();
        var response = await _http.PutAsJsonAsync($"/api/notion/synced-blocks/{syncId}/children", payload);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<(string PageId, string BlockId)>> GetAllSyncRefsAsync(string syncId)
    {
        var refs = await _http.GetFromJsonAsync<List<SyncedBlockRefLocation>>($"/api/notion/synced-blocks/{syncId}/refs");
        return refs?.Select(location => (location.PageId, location.BlockId)).ToList() ?? [];
    }

    public async Task<IPageBlock> CreateSyncRefAsync(string syncId, string targetPageId, string? afterBlockId)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/notion/synced-blocks/{syncId}/refs",
            new CreateSyncRefRequest(targetPageId, afterBlockId));
        response.EnsureSuccessStatusCode();
        var block = await response.Content.ReadFromJsonAsync<PageBlock>();
        return block ?? throw new InvalidOperationException("Synced block provider returned an empty create response.");
    }

    public async Task<IPageBlock> UnsyncBlockAsync(string blockId)
    {
        var response = await _http.PostAsync($"/api/notion/synced-blocks/refs/{blockId}/unsync", null);
        response.EnsureSuccessStatusCode();
        var block = await response.Content.ReadFromJsonAsync<PageBlock>();
        return block ?? throw new InvalidOperationException("Synced block provider returned an empty unsync response.");
    }
}
