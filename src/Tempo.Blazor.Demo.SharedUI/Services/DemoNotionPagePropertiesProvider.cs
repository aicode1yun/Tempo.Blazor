using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionPagePropertiesProvider : INotionPagePropertiesProvider
{
    private readonly HttpClient _http;

    public DemoNotionPagePropertiesProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IReadOnlyList<PagePropertiesReportRow>> QueryPagePropertiesAsync(
        PagePropertiesReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/page-properties/report", query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PagePropertiesReportRow>>(cancellationToken)
            ?? [];
    }
}
