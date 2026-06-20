using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public sealed class NotionSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_FiltersByAuthorLabelDatesContentTypeAndSpace()
    {
        var dataStore = new MockNotionDataStore();
        var blockStore = new MockNotionBlockStore();
        dataStore.SeedE2ESearchPage();
        blockStore.SeedE2ESearchPage();
        var search = new DemoNotionSearchService(dataStore, blockStore);

        var result = await search.SearchAsync(new NotionSearchRequest
        {
            Query = "beacon",
            Filter = new NotionSearchFilter
            {
                Author = "alice",
                LabelFilter = "engineering",
                CreatedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastEditedBefore = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                ContentType = "Paragraph",
                SpaceId = "CF22 Knowledge Space"
            },
            MaxResults = 10
        });

        result.Pages.Should().BeEmpty();
        result.Blocks.Should().ContainSingle(block =>
            block.BlockType == BlockType.Paragraph &&
            block.MatchSnippet.Contains("beacon", StringComparison.OrdinalIgnoreCase) &&
            block.HighlightRanges.Count == 1);
    }

    [Fact]
    public async Task SearchAsync_SupportsAccentInsensitiveQueryAndSpaceIsolation()
    {
        var dataStore = new MockNotionDataStore();
        var blockStore = new MockNotionBlockStore();
        dataStore.SeedE2ESearchPage();
        blockStore.SeedE2ESearchPage();
        var search = new DemoNotionSearchService(dataStore, blockStore);

        var accentResult = await search.SearchAsync(new NotionSearchRequest
        {
            Query = "zlutoucky",
            Filter = new NotionSearchFilter { LabelFilter = "product" },
            MaxResults = 10
        });

        accentResult.Blocks.Should().ContainSingle(block =>
            block.PageTitle == "CF22 Produktová strategie" &&
            block.MatchSnippet.Contains("žluťoučký", StringComparison.Ordinal));

        var supportResult = await search.SearchAsync(new NotionSearchRequest
        {
            Query = "customer",
            Filter = new NotionSearchFilter { SpaceId = "CF22 Support Space" },
            MaxResults = 10
        });

        supportResult.Pages.Should().OnlyContain(page => page.Title.Contains("Support", StringComparison.OrdinalIgnoreCase) || page.Title.Contains("Escalation", StringComparison.OrdinalIgnoreCase));
        supportResult.Blocks.Should().ContainSingle(block => block.PageTitle == "CF22 Escalation Notes");
    }
}
