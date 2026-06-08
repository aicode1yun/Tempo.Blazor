using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public sealed class NotionPageBulkOperationTests
{
    [Fact]
    public async Task CopyPageTreeAsync_PreservesStructureAndCopiesBlocks()
    {
        var dataStore = new MockNotionDataStore();
        var blockStore = new MockNotionBlockStore();
        dataStore.SeedE2EBulkPages();
        blockStore.SeedE2EBulkPages();

        var result = await dataStore.CopyPageTreeAsync(
            MockNotionDataStore.Page1Id.ToString("D"),
            MockNotionDataStore.Page4Id.ToString("D"));
        blockStore.CopyBlocksForPages(result.PageIdMap);

        var allPages = dataStore.GetAllPages().Cast<NotionPage>().ToList();
        var copiedRoot = result.RootPage;
        var copiedChild = allPages.Should().ContainSingle(page =>
            page.ParentId == copiedRoot.Id &&
            page.Title == "CF24 Child A").Subject;
        var copiedGrandchild = allPages.Should().ContainSingle(page =>
            page.ParentId == copiedChild.Id &&
            page.Title == "CF24 Grandchild A1").Subject;

        copiedRoot.ParentId.Should().Be(MockNotionDataStore.Page4Id);
        copiedRoot.Title.Should().Be("CF24 Source Root (Copy)");
        result.PageIdMap.Should().ContainKey(MockNotionDataStore.Page1Id);
        result.PageIdMap.Should().ContainKey(MockNotionDataStore.Page2Id);
        result.PageIdMap.Should().ContainKey(MockNotionDataStore.Page3Id);

        await AssertSameTopLevelBlockCountAsync(blockStore, MockNotionDataStore.Page1Id, copiedRoot.Id);
        await AssertSameTopLevelBlockCountAsync(blockStore, MockNotionDataStore.Page2Id, copiedChild.Id);
        await AssertSameTopLevelBlockCountAsync(blockStore, MockNotionDataStore.Page3Id, copiedGrandchild.Id);
    }

    [Fact]
    public async Task MovePagesAsync_RejectsMoveIntoDescendantWithoutPartialMutation()
    {
        var dataStore = new MockNotionDataStore();
        dataStore.SeedE2EBulkPages();

        var action = async () => await dataStore.MovePagesAsync(
            [MockNotionDataStore.Page1Id.ToString("D")],
            MockNotionDataStore.Page3Id.ToString("D"));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*descendants*");

        var source = (NotionPage)await dataStore.GetPageAsync(MockNotionDataStore.Page1Id.ToString("D"));
        source.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task DeletePagesAsync_MovesSelectedPagesAndDescendantsToTrash()
    {
        var dataStore = new MockNotionDataStore();
        dataStore.SeedE2EBulkPages();

        await dataStore.DeletePagesAsync([MockNotionDataStore.Page1Id.ToString("D")]);

        var trashedIds = (await dataStore.GetTrashAsync())
            .Select(page => page.Id)
            .ToList();

        trashedIds.Should().Contain(MockNotionDataStore.Page1Id);
        trashedIds.Should().Contain(MockNotionDataStore.Page2Id);
        trashedIds.Should().Contain(MockNotionDataStore.Page3Id);
        trashedIds.Should().NotContain(MockNotionDataStore.Page4Id);
    }

    private static async Task AssertSameTopLevelBlockCountAsync(MockNotionBlockStore blockStore, Guid sourcePageId, Guid copiedPageId)
    {
        var sourceBlocks = (await blockStore.GetBlocksAsync(sourcePageId.ToString("D"))).Cast<IPageBlock>().ToList();
        var copiedBlocks = (await blockStore.GetBlocksAsync(copiedPageId.ToString("D"))).Cast<IPageBlock>().ToList();

        copiedBlocks.Should().HaveCount(sourceBlocks.Count);
        copiedBlocks.Select(block => block.Type).Should().Equal(sourceBlocks.Select(block => block.Type));
        copiedBlocks.Select(block => block.Id).Should().NotIntersectWith(sourceBlocks.Select(block => block.Id));
    }
}
