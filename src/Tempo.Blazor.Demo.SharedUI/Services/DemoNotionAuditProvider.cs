using System.Net.Http.Json;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionAuditProvider : ITmActivityProvider
{
    private readonly HttpClient _http;

    public DemoNotionAuditProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public TmActivityProviderCapabilities Capabilities
        => TmActivityProviderCapabilities.Read
        | TmActivityProviderCapabilities.Query
        | TmActivityProviderCapabilities.Append;

    TmActivityProviderCapabilities ITmCapabilityProvider<TmActivityProviderCapabilities>.Capabilities => Capabilities;

    public async Task<IReadOnlyList<TmActivityEntry>> GetForEntityAsync(
        TmEntityRef entityRef,
        CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(new TmActivityQuery
        {
            EntityRef = entityRef,
            Take = 100
        }, cancellationToken);

        return result.Items;
    }

    public async Task<TmActivityEntry> AppendAsync(TmActivityEntry entry, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/api/notion/audit/entries", entry, cancellationToken);
        response.EnsureSuccessStatusCode();
        return entry;
    }

    public async Task<PagedResult<TmActivityEntry>> QueryAsync(
        TmActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string>
        {
            $"skip={Math.Max(0, query.Skip)}",
            $"take={Math.Clamp(query.Take, 1, 100)}"
        };

        AddQuery(parameters, "userId", query.ActorId ?? query.SearchText);
        AddQuery(parameters, "action", query.Action);
        AddQuery(parameters, "targetType", query.EntityRef?.EntityType ?? query.EntityType);
        AddQuery(parameters, "targetId", query.EntityRef?.EntityId ?? query.EntityId);
        AddQuery(parameters, "correlationId", query.CorrelationId);

        if (query.From is { } from)
            parameters.Add($"from={Uri.EscapeDataString(from.ToString("O"))}");

        if (query.To is { } to)
            parameters.Add($"to={Uri.EscapeDataString(to.ToString("O"))}");

        using var response = await _http.GetAsync($"/api/notion/audit/entries?{string.Join("&", parameters)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<TmActivityEntry>>(cancellationToken) ?? new PagedResult<TmActivityEntry>();
    }

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }
}
