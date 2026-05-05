using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public class DemoNotionDataProvider : INotionDataProvider
{
    private readonly HttpClient _http;

    public DemoNotionDataProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<INotionPage> GetPageAsync(string pageId)
    {
        var page = await _http.GetFromJsonAsync<NotionPage>($"/api/notion/pages/{pageId}");
        return page ?? throw new KeyNotFoundException($"Page {pageId} not found");
    }

    public async Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
    {
        var url = parentId == null
            ? "/api/notion/pages/root/children"
            : $"/api/notion/pages/{parentId}/children";
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>(url);
        return pages ?? [];
    }

    public async Task<IEnumerable<INotionPage>> GetFavoritesAsync()
    {
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>("/api/notion/pages/favorites");
        return pages ?? [];
    }

    public async Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
    {
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>($"/api/notion/pages/recent/{count}");
        return pages ?? [];
    }

    public async Task<IEnumerable<INotionPage>> GetTrashAsync()
    {
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>("/api/notion/pages/trash");
        return pages ?? [];
    }

    public async Task<INotionPage> CreatePageAsync(string? parentId, string title)
    {
        var request = new { title, parentId };
        var response = await _http.PostAsJsonAsync("/api/notion/pages", request);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<NotionPage>();
        return page ?? throw new Exception("Failed to create page");
    }

    public async Task UpdatePageAsync(INotionPage page)
    {
        if (page is NotionPage notionPage)
        {
            var request = new
            {
                notionPage.Title,
                notionPage.Description,
                notionPage.IconEmoji
            };
            var response = await _http.PutAsJsonAsync($"/api/notion/pages/{page.Id}", request);
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task DeletePageAsync(string pageId)
    {
        var response = await _http.DeleteAsync($"/api/notion/pages/{pageId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task RestorePageAsync(string pageId)
    {
        await _http.PostAsync($"/api/notion/pages/{pageId}/restore", null);
    }

    public async Task PermanentlyDeletePageAsync(string pageId)
    {
        var response = await _http.DeleteAsync($"/api/notion/pages/{pageId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task ToggleFavoriteAsync(string pageId, bool isFavorite)
    {
        var response = await _http.PostAsync($"/api/notion/pages/{pageId}/favorite/{isFavorite}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task MovePageAsync(string pageId, string? newParentId)
    {
        await _http.PostAsJsonAsync($"/api/notion/pages/{pageId}/move", new { newParentId });
    }

    public async Task<INotionPage> DuplicatePageAsync(string pageId)
    {
        var response = await _http.PostAsync($"/api/notion/pages/{pageId}/duplicate", null);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<NotionPage>();
        return page ?? throw new Exception("Failed to duplicate page");
    }
}
