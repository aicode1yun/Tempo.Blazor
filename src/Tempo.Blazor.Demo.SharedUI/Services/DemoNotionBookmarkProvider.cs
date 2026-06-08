using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionBookmarkProvider : INotionBookmarkProvider
{
    private readonly HttpClient _http;

    public DemoNotionBookmarkProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IBookmarkBlockContent> ResolveBookmarkAsync(string url)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/bookmarks/resolve", new BookmarkResolveRequest(url));
        response.EnsureSuccessStatusCode();
        var bookmark = await response.Content.ReadFromJsonAsync<BookmarkBlockContent>();
        return bookmark ?? throw new InvalidOperationException("Bookmark resolver returned an empty response.");
    }
}
