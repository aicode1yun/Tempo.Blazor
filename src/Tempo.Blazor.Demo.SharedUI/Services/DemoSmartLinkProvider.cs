using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoSmartLinkProvider : ISmartLinkProvider
{
    private readonly HttpClient _http;

    public DemoSmartLinkProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<SmartLinkDto?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "/api/notion/smart-links/resolve",
            new SmartLinkResolveRequest(url),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SmartLinkDto>(cancellationToken);
    }
}
