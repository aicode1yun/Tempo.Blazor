using System.Net.Http.Json;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Demo.Services;

/// <summary>HTTP-backed unified work-item provider for the demo Notion task source.</summary>
public sealed class DemoNotionTaskProvider : TmWorkItemProviderBase
{
    private readonly HttpClient _http;

    public DemoNotionTaskProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public override string SourceKey => "notion";

    public override string DisplayName => "Notion tasks";

    public override TmWorkItemCapabilities Capabilities =>
        TmWorkItemCapabilities.Read | TmWorkItemCapabilities.Update;

    public override async Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/tasks/query", query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<TmWorkItem>>(cancellationToken)
            ?? new PagedResult<TmWorkItem> { Items = [], TotalCount = 0, Page = 1, PageSize = query.Take <= 0 ? 50 : query.Take };
    }

    public override async Task SetCompletedAsync(string id, bool completed, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/tasks/{id}/completed", new { completed }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
