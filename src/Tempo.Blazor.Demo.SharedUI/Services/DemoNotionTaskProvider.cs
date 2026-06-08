using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionTaskProvider : INotionTaskProvider
{
    private readonly HttpClient _http;

    public DemoNotionTaskProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<PagedResult<NotionTaskDto>> GetTasksAsync(NotionTaskQuery query, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/api/notion/tasks/query", query, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<NotionTaskDto>>(cancellationToken)
            ?? new PagedResult<NotionTaskDto> { Items = [], TotalCount = 0, Page = 1, PageSize = query.Take <= 0 ? 50 : query.Take };
    }

    public async Task SetCompletedAsync(string taskId, bool completed, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/tasks/{taskId}/completed", new { completed }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
