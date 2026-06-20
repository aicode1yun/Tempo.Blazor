using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionReactionProvider : INotionReactionProvider
{
    private readonly HttpClient _http;

    public DemoNotionReactionProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IReadOnlyList<PageReactionDto>> GetReactionsAsync(Guid pageId, CancellationToken cancellationToken = default)
        => await _http.GetFromJsonAsync<IReadOnlyList<PageReactionDto>>($"/api/notion/reactions/pages/{pageId:D}", cancellationToken)
            ?? [];

    public async Task<IReadOnlyList<PageReactionDto>> ToggleLikeAsync(Guid pageId, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/notion/reactions/pages/{pageId:D}/like",
            new PageReactionToggleRequest { UserId = userId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PageReactionDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<PageReactionDto>> ToggleReactionAsync(Guid pageId, string reaction, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/notion/reactions/pages/{pageId:D}/reaction",
            new PageReactionToggleRequest { UserId = userId, Reaction = reaction },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PageReactionDto>>(cancellationToken) ?? [];
    }
}
