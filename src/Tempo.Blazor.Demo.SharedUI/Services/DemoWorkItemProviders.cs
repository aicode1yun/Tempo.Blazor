using System.Net.Http.Json;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Models;

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

public abstract class HttpWorkItemProvider : TmWorkItemProviderBase
{
    private readonly HttpClient _http;

    protected HttpWorkItemProvider(IHttpClientFactory factory, string sourceKey, string displayName)
    {
        _http = factory.CreateClient("DemoApi");
        SourceKey = sourceKey;
        DisplayName = displayName;
    }

    public override string SourceKey { get; }
    public override string DisplayName { get; }
    public override TmWorkItemCapabilities Capabilities => TmWorkItemCapabilities.Read;

    public override async Task<TmWorkItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync(
            $"/api/notion/work-items/{Uri.EscapeDataString(SourceKey)}/{Uri.EscapeDataString(id)}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TmWorkItem>(cancellationToken);
    }

    public override async Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = new TmWorkItemQuery
        {
            SourceKey = SourceKey,
            FreeText = query.FreeText,
            Ids = query.Ids,
            QueryString = query.QueryString,
            Skip = query.Skip,
            Take = query.Take
        };

        var response = await _http.PostAsJsonAsync("/api/notion/work-items/query", normalized, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<TmWorkItem>>(cancellationToken)
            ?? new PagedResult<TmWorkItem> { Items = [], TotalCount = 0, Page = 1, PageSize = normalized.Take };
    }
}
