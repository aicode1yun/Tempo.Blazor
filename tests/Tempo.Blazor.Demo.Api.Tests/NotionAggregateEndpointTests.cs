using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public sealed class NotionAggregateEndpointTests :
    IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid PageId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly HttpClient _client;

    public NotionAggregateEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task AtomicTable_SaveReplacesCompleteSnapshot_AndRejectsStaleToken()
    {
        (await _client.PostAsync(
            "/api/notion/e2e/seed/seedAtomicTablePage",
            null)).EnsureSuccessStatusCode();

        var load = await _client.GetFromJsonAsync<NotionAggregateLoadResult>(
            $"/api/notion/aggregate/pages/{PageId:D}",
            NotionAggregateJson.Options);
        Assert.NotNull(load?.Snapshot);
        Assert.Empty(NotionAggregateValidator.Validate([load.Snapshot]));

        var row = load.Snapshot.Blocks
            .First(block => block.Type == BlockType.TableRow);
        var content = row.Content.Deserialize<NotionAuthoringTableRow>(
            NotionAggregateJson.Options)!;
        content.Cells[0].Html = "<strong>Saved atomically</strong>";
        row.Content = JsonSerializer.SerializeToElement(
            content,
            NotionAggregateJson.Options);

        var request = new NotionAggregateSaveRequest
        {
            Pages =
            [
                new NotionPageSave
                {
                    Snapshot = load.Snapshot,
                    BaseConcurrencyToken = load.Snapshot.ConcurrencyToken
                }
            ]
        };
        var savedResponse = await _client.PostAsJsonAsync(
            "/api/notion/aggregate/save",
            request,
            NotionAggregateJson.Options);
        savedResponse.EnsureSuccessStatusCode();
        var saved = await savedResponse.Content.ReadFromJsonAsync<NotionAggregateSaveResult>(
            NotionAggregateJson.Options);
        Assert.True(saved?.Success);
        Assert.Single(saved!.Pages);

        var staleResponse = await _client.PostAsJsonAsync(
            "/api/notion/aggregate/save",
            request,
            NotionAggregateJson.Options);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        var conflict = await staleResponse.Content.ReadFromJsonAsync<NotionAggregateSaveResult>(
            NotionAggregateJson.Options);
        Assert.True(conflict?.Conflict);
        Assert.Single(conflict!.Conflicts);
    }

    [Fact]
    public async Task DefaultDemoPage_ConvertsToAValidAggregate()
    {
        (await _client.PostAsync("/api/notion/reset", null)).EnsureSuccessStatusCode();
        var load = await _client.GetFromJsonAsync<NotionAggregateLoadResult>(
            $"/api/notion/aggregate/pages/{PageId:D}",
            NotionAggregateJson.Options);

        Assert.NotNull(load?.Snapshot);
        var issues = NotionAggregateValidator.Validate([load.Snapshot]);
        Assert.True(
            issues.Count == 0,
            string.Join(
                Environment.NewLine,
                issues.Select(issue => $"{issue.Code} {issue.Path}: {issue.Message}")));
    }
}
