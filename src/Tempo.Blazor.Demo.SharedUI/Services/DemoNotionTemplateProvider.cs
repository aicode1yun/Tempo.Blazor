using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionTemplateProvider : INotionTemplateProvider
{
    private readonly HttpClient _http;

    public DemoNotionTemplateProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<IReadOnlyList<NotionTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        => await _http.GetFromJsonAsync<IReadOnlyList<NotionTemplateDto>>("/api/notion/templates", cancellationToken)
           ?? [];

    public async Task<NotionTemplateDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync($"/api/notion/templates/{Uri.EscapeDataString(id)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotionTemplateDto>(cancellationToken);
    }
}
