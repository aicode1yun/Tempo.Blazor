using FluentAssertions;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Demo.Api.Data;

namespace Tempo.Blazor.Demo.Api.Tests;

public sealed class WorkItemStoreTests
{
    [Fact]
    public void Search_WithFreeTextAndNoIds_ReturnsTitleMatches()
    {
        var store = new DemoWorkItemStore();

        var result = store.Search(new TmWorkItemQuery
        {
            SourceKey = "demo",
            FreeText = "release",
            Ids = []
        });

        result.Items.Should().Contain(item => item.Title.Contains("release", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_WithFreeTextAndNonMatchingId_AndsFiltersAndReturnsNoMatches()
    {
        var store = new DemoWorkItemStore();

        var result = store.Search(new TmWorkItemQuery
        {
            SourceKey = "demo",
            FreeText = "release",
            Ids = ["release"]
        });

        result.Items.Should().BeEmpty();
    }
}
