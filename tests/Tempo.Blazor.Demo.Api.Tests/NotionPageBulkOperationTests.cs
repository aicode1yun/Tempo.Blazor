using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

public sealed class NotionPageBulkOperationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NotionPageBulkOperationTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

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

        var visibleRootIds = (await dataStore.GetChildPagesAsync(null))
            .Select(page => page.Id)
            .ToList();
        var visibleSpaceIds = (await dataStore.GetPagesInSpaceAsync("cf24-source"))
            .Select(page => page.Id)
            .ToList();

        visibleRootIds.Should().NotContain(MockNotionDataStore.Page1Id);
        visibleSpaceIds.Should().NotContain(MockNotionDataStore.Page1Id);
        visibleSpaceIds.Should().NotContain(MockNotionDataStore.Page2Id);
        visibleSpaceIds.Should().NotContain(MockNotionDataStore.Page3Id);
    }

    [Fact]
    public async Task BulkPageEndpoints_MoveCopyTreeAndDeleteThroughHttpContract()
    {
        var seed = await _client.PostAsync("/api/notion/e2e/seed/seedBulkPages", null);
        seed.EnsureSuccessStatusCode();

        var copy = await _client.PostAsJsonAsync(
            $"/api/notion/pages/{MockNotionDataStore.Page1Id:D}/copy-tree",
            new CopyPageTreeRequest(MockNotionDataStore.Page4Id.ToString("D")));
        copy.StatusCode.Should().Be(HttpStatusCode.Created);

        var copiedRoot = await copy.Content.ReadFromJsonAsync<NotionPage>();
        copiedRoot.Should().NotBeNull();
        copiedRoot!.ParentId.Should().Be(MockNotionDataStore.Page4Id);
        copiedRoot.Title.Should().Be("CF24 Source Root (Copy)");

        var copiedChildren = await _client.GetFromJsonAsync<List<NotionPage>>($"/api/notion/pages/{copiedRoot.Id:D}/children");
        copiedChildren.Should().ContainSingle(page => page.Title == "CF24 Child A");

        var delete = await _client.PostAsJsonAsync(
            "/api/notion/pages/bulk/delete",
            new BulkDeletePagesRequest([copiedRoot.Id.ToString("D")]));
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var trash = await _client.GetFromJsonAsync<List<NotionPage>>("/api/notion/pages/trash");
        trash.Should().Contain(page => page.Id == copiedRoot.Id);
        trash.Should().Contain(page => page.Title == "CF24 Child A");

        var move = await _client.PostAsJsonAsync(
            "/api/notion/pages/bulk/move",
            new BulkMovePagesRequest([MockNotionDataStore.Page2Id.ToString("D")], null));
        move.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var moved = await _client.GetFromJsonAsync<NotionPage>($"/api/notion/pages/{MockNotionDataStore.Page2Id:D}");
        moved.Should().NotBeNull();
        moved!.ParentId.Should().BeNull();
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
