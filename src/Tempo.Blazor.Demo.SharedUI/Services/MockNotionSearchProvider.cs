using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Search provider that fetches all pages from the Demo API and filters them
/// client-side using case-insensitive string.Contains. Block-level search is not
/// implemented (returns empty) to avoid per-page HTTP round-trips in the demo.
/// </summary>
public class MockNotionSearchProvider : INotionSearchProvider
{
    private readonly HttpClient _http;

    public MockNotionSearchProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IEnumerable<INotionPage>> SearchPagesAsync(string query, NotionSearchFilter? filter)
    {
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>("/api/notion/pages") ?? [];
        return Filter(pages, query, filter).Cast<INotionPage>();
    }

    public Task<IEnumerable<NotionSearchResult>> SearchBlocksAsync(string query, NotionSearchFilter? filter)
        => Task.FromResult<IEnumerable<NotionSearchResult>>(Array.Empty<NotionSearchResult>());

    public async Task<(IEnumerable<INotionPage> Pages, IEnumerable<NotionSearchResult> Blocks)> SearchAllAsync(
        string query, NotionSearchFilter? filter, int maxResults)
    {
        var pages = await SearchPagesAsync(query, filter);
        return (pages.Take(maxResults), Array.Empty<NotionSearchResult>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<NotionPage> Filter(List<NotionPage> pages, string query, NotionSearchFilter? filter)
    {
        var q = pages.Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (filter is not null)
        {
            if (filter.CreatedAfter  is { } after)  q = q.Where(p => p.CreatedAt >= after);
            if (filter.CreatedBefore is { } before)  q = q.Where(p => p.CreatedAt <= before);
            if (filter.CreatedByUserId is { } uid)   q = q.Where(p => p.CreatedByUserId == uid);
        }

        return q.OrderByDescending(p => p.LastEditedAt);
    }
}
