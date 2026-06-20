using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoWorkItemProvider : HttpWorkItemProvider
{
    public DemoWorkItemProvider(IHttpClientFactory factory)
        : base(factory, "demo", "Demo tracker")
    {
    }
}

public sealed class DemoOpsWorkItemProvider : HttpWorkItemProvider
{
    public DemoOpsWorkItemProvider(IHttpClientFactory factory)
        : base(factory, "ops", "Ops tracker")
    {
    }
}

public abstract class HttpWorkItemProvider : IWorkItemProvider
{
    private readonly HttpClient _http;

    protected HttpWorkItemProvider(IHttpClientFactory factory, string providerKey, string displayName)
    {
        _http = factory.CreateClient("DemoApi");
        ProviderKey = providerKey;
        DisplayName = displayName;
    }

    public string ProviderKey { get; }
    public string DisplayName { get; }

    public async Task<WorkItemDto?> GetByIdAsync(string externalId, CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync(
            $"/api/notion/work-items/{Uri.EscapeDataString(ProviderKey)}/{Uri.EscapeDataString(externalId)}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkItemDto>(cancellationToken);
    }

    public async Task<PagedResult<WorkItemDto>> SearchAsync(WorkItemQuery query, CancellationToken cancellationToken)
    {
        var normalized = new WorkItemQuery
        {
            ProviderKey = ProviderKey,
            FreeText = query.FreeText,
            Ids = query.Ids,
            QueryString = query.QueryString,
            Jql = query.Jql,
            Skip = query.Skip,
            Take = query.Take
        };

        var response = await _http.PostAsJsonAsync("/api/notion/work-items/query", normalized, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<WorkItemDto>>(cancellationToken)
            ?? new PagedResult<WorkItemDto> { Items = [], TotalCount = 0, Page = 1, PageSize = normalized.Take };
    }
}
