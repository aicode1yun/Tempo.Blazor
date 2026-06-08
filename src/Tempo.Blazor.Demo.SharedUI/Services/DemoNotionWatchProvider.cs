using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionWatchProvider : INotionWatchProvider
{
    private readonly HttpClient _http;

    public DemoNotionWatchProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task WatchAsync(string pageId, string userId, bool includeChildren, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync(
            $"/api/notion/watches/pages/{Uri.EscapeDataString(pageId)}",
            new NotionWatchRequest { UserId = userId, IncludeChildren = includeChildren },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnwatchAsync(string pageId, string userId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync(
            $"/api/notion/watches/pages/{Uri.EscapeDataString(pageId)}/users/{Uri.EscapeDataString(userId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<NotionWatchSubscriptionDto>> GetWatchersAsync(string pageId, CancellationToken cancellationToken = default)
    {
        var watchers = await _http.GetFromJsonAsync<List<NotionWatchSubscriptionDto>>(
            $"/api/notion/watches/pages/{Uri.EscapeDataString(pageId)}",
            cancellationToken);
        return watchers ?? [];
    }

    public async Task<bool> IsWatchingAsync(string pageId, string userId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"/api/notion/watches/pages/{Uri.EscapeDataString(pageId)}/users/{Uri.EscapeDataString(userId)}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken);
    }
}
