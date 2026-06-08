using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionSpaceProvider : INotionSpaceProvider
{
    private readonly HttpClient _http;

    public DemoNotionSpaceProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IReadOnlyList<NotionSpaceDto>> GetSpacesAsync(CancellationToken cancellationToken = default)
        => await _http.GetFromJsonAsync<List<NotionSpaceDto>>("/api/notion/spaces", cancellationToken) ?? [];

    public async Task<NotionSpaceDto?> GetSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/api/notion/spaces/{Uri.EscapeDataString(spaceId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionSpaceDto>(cancellationToken);
    }

    public async Task<NotionSpaceDto> CreateSpaceAsync(NotionSpaceDto space, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/spaces", space, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionSpaceDto>(cancellationToken)
               ?? throw new InvalidOperationException("The demo API did not return the created space.");
    }

    public async Task<IReadOnlyList<INotionPage>> GetPagesInSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
    {
        var pages = await _http.GetFromJsonAsync<List<NotionPage>>(
            $"/api/notion/spaces/{Uri.EscapeDataString(spaceId)}/pages",
            cancellationToken);

        return pages?.Cast<INotionPage>().ToArray() ?? [];
    }

    public async Task MovePageToSpaceAsync(string pageId, string spaceId, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/notion/spaces/pages/{Uri.EscapeDataString(pageId)}/move",
            new MovePageToSpaceRequest(spaceId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
