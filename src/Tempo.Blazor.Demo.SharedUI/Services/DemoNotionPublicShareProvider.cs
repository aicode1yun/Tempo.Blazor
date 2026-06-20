using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionPublicShareProvider : INotionPublicShareProvider
{
    private readonly HttpClient _http;

    public DemoNotionPublicShareProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<PublicShareDto> CreateShareAsync(Guid pageId, PublicShareOptions options, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"/api/notion/public-shares/pages/{pageId:D}", options, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PublicShareDto>(cancellationToken)
            ?? throw new InvalidOperationException("The public share response was empty.");
    }

    public async Task RevokeAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync($"/api/notion/public-shares/pages/{pageId:D}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PublicShareDto?> GetShareAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/api/notion/public-shares/pages/{pageId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PublicShareDto>(cancellationToken);
    }

    public async Task<PublicShareDto?> ResolveByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/api/notion/public-shares/tokens/{Uri.EscapeDataString(token)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PublicShareDto>(cancellationToken);
    }
}
