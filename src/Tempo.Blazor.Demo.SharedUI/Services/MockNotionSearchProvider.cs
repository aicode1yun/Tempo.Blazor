using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// Search provider backed by the Demo API full-text endpoint.
/// </summary>
public class MockNotionSearchProvider : INotionSearchProvider
{
    private readonly HttpClient _http;

    public MockNotionSearchProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IEnumerable<INotionPage>> SearchPagesAsync(string query, NotionSearchFilter? filter)
        => (await SearchAsync(query, filter, 20)).Pages.Cast<INotionPage>();

    public async Task<IEnumerable<NotionSearchResult>> SearchBlocksAsync(string query, NotionSearchFilter? filter)
        => (await SearchAsync(query, filter, 20)).Blocks;

    public async Task<(IEnumerable<INotionPage> Pages, IEnumerable<NotionSearchResult> Blocks)> SearchAllAsync(
        string query, NotionSearchFilter? filter, int maxResults)
    {
        var response = await SearchAsync(query, filter, maxResults);
        return (response.Pages.Cast<INotionPage>(), response.Blocks);
    }

    private async Task<NotionSearchResponse> SearchAsync(string query, NotionSearchFilter? filter, int maxResults)
    {
        var request = new NotionSearchRequest
        {
            Query = query,
            Filter = filter,
            MaxResults = maxResults
        };
        var response = await _http.PostAsJsonAsync("/api/notion/search", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionSearchResponse>() ?? new NotionSearchResponse();
    }
}
