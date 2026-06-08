using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionSearchFilterContractTests
{
    [Fact]
    public void NotionSearchFilter_RoundTripsAdvancedOptionalFilters()
    {
        var filter = new NotionSearchFilter
        {
            Author = "alice",
            LabelFilter = "engineering",
            CreatedAfter = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBefore = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            LastEditedAfter = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            LastEditedBefore = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            ContentType = "Paragraph",
            SpaceId = "knowledge"
        };

        var restored = JsonSerializer.Deserialize<NotionSearchFilter>(JsonSerializer.Serialize(filter));

        restored.Should().BeEquivalentTo(filter);
    }

    [Fact]
    public void NotionSearchResult_RoundTripsSerializableHighlightRanges()
    {
        var result = new NotionSearchResult
        {
            PageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PageTitle = "Search page",
            MatchSnippet = "Architecture beacon release",
            HighlightRanges = [new NotionSearchHighlightRange(13, 19)]
        };

        var restored = JsonSerializer.Deserialize<NotionSearchResult>(JsonSerializer.Serialize(result));

        restored!.HighlightRanges.Should().ContainSingle(range => range.Start == 13 && range.End == 19);
    }
}
