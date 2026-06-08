using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionAuditProvider : INotionAuditProvider
{
    private readonly HttpClient _http;

    public DemoNotionAuditProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task LogAsync(AuditEntryDto entry, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/api/notion/audit/entries", entry, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResult<AuditEntryDto>> GetEntriesAsync(
        AuditLogFilter filter,
        NotionAuditQuery paging,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"skip={Math.Max(0, paging.Skip)}",
            $"take={Math.Clamp(paging.Take, 1, 100)}"
        };

        AddQuery(query, "userId", filter.UserId);
        AddQuery(query, "action", filter.Action);
        AddQuery(query, "targetType", filter.TargetType);
        AddQuery(query, "targetId", filter.TargetId);

        if (filter.From is { } from)
            query.Add($"from={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}");

        if (filter.To is { } to)
            query.Add($"to={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}");

        using var response = await _http.GetAsync($"/api/notion/audit/entries?{string.Join("&", query)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<AuditEntryDto>>(cancellationToken) ?? new PagedResult<AuditEntryDto>();
    }

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }
}
