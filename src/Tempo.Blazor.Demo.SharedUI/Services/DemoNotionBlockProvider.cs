using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public class DemoNotionBlockProvider : INotionBlockProvider
{
    private readonly HttpClient _http;

    public DemoNotionBlockProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
    {
        var blocks = await _http.GetFromJsonAsync<List<PageBlock>>($"/api/notion/blocks/page/{pageId}");
        return blocks ?? [];
    }

    public async Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
    {
        var blocks = await _http.GetFromJsonAsync<List<PageBlock>>($"/api/notion/blocks/parent/{parentBlockId}");
        return blocks ?? [];
    }

    public async Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
    {
        var request = new { pageId, block, afterBlockId };
        var response = await _http.PostAsJsonAsync("/api/notion/blocks", request);
        response.EnsureSuccessStatusCode();
        var createdBlock = await response.Content.ReadFromJsonAsync<PageBlock>();
        return createdBlock ?? throw new Exception("Failed to create block");
    }

    public async Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
    {
        var request = new { pageId, blocks, afterBlockId };
        var response = await _http.PostAsJsonAsync("/api/notion/blocks/batch", request);
        response.EnsureSuccessStatusCode();
        var createdBlocks = await response.Content.ReadFromJsonAsync<List<PageBlock>>();
        return createdBlocks ?? [];
    }

    public async Task RestoreBlocksAsync(IEnumerable<IPageBlock> blocks)
    {
        var payload = blocks.ToList();
        if (payload.Count == 0) return;

        var response = await _http.PostAsJsonAsync("/api/notion/blocks/restore", payload);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateBlockAsync(IPageBlock block)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/blocks/{block.Id}", block);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteBlockAsync(string blockId)
    {
        var response = await _http.DeleteAsync($"/api/notion/blocks/{blockId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds)
    {
        var request = new { pageId, orderedBlockIds };
        var response = await _http.PostAsJsonAsync("/api/notion/blocks/reorder", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveBlockAsync(MoveNotionBlockRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/blocks/move", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/blocks/{blockId}/move-to-page", new MoveBlockToPageRequest(targetPageId, afterBlockId));
        response.EnsureSuccessStatusCode();
    }

    public async Task<IPageBlock> DuplicateBlockAsync(string blockId)
    {
        var response = await _http.PostAsync($"/api/notion/blocks/{blockId}/duplicate", null);
        response.EnsureSuccessStatusCode();
        var block = await response.Content.ReadFromJsonAsync<PageBlock>();
        return block ?? throw new Exception("Failed to duplicate block");
    }

    public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType)
        => ConvertBlockTypeAsync(blockId, newType, currentHtml: null);

    public async Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType, string? currentHtml)
    {
        var response = await _http.PostAsJsonAsync($"/api/notion/blocks/{blockId}/convert", new { newType, currentHtml });
        response.EnsureSuccessStatusCode();
        var block = await response.Content.ReadFromJsonAsync<PageBlock>();
        return block ?? throw new Exception("Failed to convert block");
    }

    public async Task<string> GetBlockLinkAsync(string blockId)
    {
        return await Task.FromResult($"https://notion.demo/block/{blockId}");
    }
}
