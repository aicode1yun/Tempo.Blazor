using System.Net.Http.Json;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Services;

public sealed class DemoNotionPermissionProvider : INotionPermissionProvider
{
    private readonly HttpClient _http;

    public DemoNotionPermissionProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<PageRestrictionDto> GetRestrictionsAsync(Guid pageId, CancellationToken cancellationToken = default)
        => await _http.GetFromJsonAsync<PageRestrictionDto>($"/api/notion/permissions/pages/{pageId:D}", cancellationToken)
           ?? new PageRestrictionDto { PageId = pageId };

    public async Task SetRestrictionsAsync(PageRestrictionDto restrictions, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"/api/notion/permissions/pages/{restrictions.PageId:D}",
            restrictions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PageEffectivePermissionDto> GetEffectivePermissionAsync(
        Guid pageId,
        string userId,
        IReadOnlyList<string>? groupIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = groupIds is { Count: > 0 }
            ? $"?groups={Uri.EscapeDataString(string.Join(',', groupIds))}"
            : string.Empty;

        return await _http.GetFromJsonAsync<PageEffectivePermissionDto>(
                   $"/api/notion/permissions/pages/{pageId:D}/effective/{Uri.EscapeDataString(userId)}{query}",
                   cancellationToken)
               ?? new PageEffectivePermissionDto
               {
                   PageId = pageId,
                   UserId = userId,
                   Permission = PageRestrictionPermission.Edit
               };
    }
}
