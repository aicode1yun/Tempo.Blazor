using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public class NotionHistoryDiffEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string PageId = "11111111-1111-1111-1111-111111111111";
    private const string CurrentVersionId = "cf230000-0000-0000-0000-000000000003";
    private const string InitialVersionId = "cf230000-0000-0000-0000-000000000001";
    private const string IdenticalInitialVersionId = "cf230000-0000-0000-0000-000000000004";

    private readonly HttpClient _client;

    public NotionHistoryDiffEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetDiff_ReturnsSerializableBlockChanges()
    {
        var seedResponse = await _client.PostAsync("/api/notion/e2e/seed/history-diff", null);
        Assert.Equal(HttpStatusCode.NoContent, seedResponse.StatusCode);

        var diffs = await _client.GetFromJsonAsync<List<BlockDiff>>(
            $"/api/notion/history/pages/{PageId}/diff?fromVersionId={InitialVersionId}&toVersionId={CurrentVersionId}");

        Assert.NotNull(diffs);
        Assert.Contains(diffs, diff => diff.DiffType == BlockDiffType.Added && diff.After?.Content is IBlockContent);
        Assert.Contains(diffs, diff => diff.DiffType == BlockDiffType.Removed && diff.Before?.Content is IBlockContent);
        Assert.Contains(diffs, diff => diff.DiffType == BlockDiffType.Modified && diff.Before is not null && diff.After is not null);
        Assert.Contains(diffs, diff => diff.DiffType == BlockDiffType.Moved && diff.BeforeOrder != diff.AfterOrder);
        Assert.True(diffs.Count >= 12);
    }

    [Fact]
    public async Task GetDiff_ReturnsEmptyListForEquivalentSnapshots()
    {
        var seedResponse = await _client.PostAsync("/api/notion/e2e/seed/history-diff", null);
        Assert.Equal(HttpStatusCode.NoContent, seedResponse.StatusCode);

        var diffs = await _client.GetFromJsonAsync<List<BlockDiff>>(
            $"/api/notion/history/pages/{PageId}/diff?fromVersionId={InitialVersionId}&toVersionId={IdenticalInitialVersionId}");

        Assert.NotNull(diffs);
        Assert.Empty(diffs);
    }
}
