using System.Net;
using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionAnalyticsProvider : INotionAnalyticsProvider
{
    private readonly HttpClient _http;

    public DemoNotionAnalyticsProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task RecordViewAsync(Guid pageId, string? userId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            $"/api/notion/analytics/pages/{pageId:D}/views",
            new RecordPageViewRequest { UserId = userId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PageAnalyticsDto?> GetPageAnalyticsAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/api/notion/analytics/pages/{pageId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PageAnalyticsDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<PageAnalyticsDto>> GetTopPagesAsync(string spaceId, NotionAnalyticsRange range, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"take={Math.Clamp(range.Take, 1, 50)}"
        };

        if (range.From is { } from)
            query.Add($"from={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}");

        if (range.To is { } to)
            query.Add($"to={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}");

        using var response = await _http.GetAsync(
            $"/api/notion/analytics/spaces/{Uri.EscapeDataString(spaceId)}/top-pages?{string.Join("&", query)}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PageAnalyticsDto>>(cancellationToken) ?? [];
    }
}
