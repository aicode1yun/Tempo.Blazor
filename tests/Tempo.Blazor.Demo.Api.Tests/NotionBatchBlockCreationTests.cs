using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// A batch insert — a pasted table, a template — arrives as a parent plus its children. The store
/// assigns fresh ids, so a child's ParentBlockId has to follow its parent's new id. Without the
/// remap the children point at an id that never reaches the store and the parent renders empty.
/// </summary>
public sealed class NotionBatchBlockCreationTests
{
    private static readonly Guid PageId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");

    [Fact]
    public async Task CreateBlocks_ReparentsChildrenOntoTheirParentsNewId()
    {
        var store = new MockNotionBlockStore();
        var clientTableId = Guid.NewGuid();

        var created = (await store.CreateBlocksAsync(PageId.ToString(), Table(clientTableId), afterBlockId: null)).ToList();

        var table = created.Single(block => block.Type == BlockType.Table);
        var rows = created.Where(block => block.Type == BlockType.TableRow).ToList();

        table.Id.Should().NotBe(clientTableId, "the store owns the ids");
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.ParentBlockId == table.Id);
    }

    [Fact]
    public async Task CreateBlocks_ChildrenAreReachableFromTheParent()
    {
        var store = new MockNotionBlockStore();

        var created = (await store.CreateBlocksAsync(PageId.ToString(), Table(Guid.NewGuid()), afterBlockId: null)).ToList();
        var table = created.Single(block => block.Type == BlockType.Table);

        var children = (await store.GetChildBlocksAsync(table.Id.ToString())).ToList();

        children.Should().HaveCount(2);
        children.Select(child => ((TableRowBlockContent)child.Content).Cells[0])
            .Should().Equal("Name", "CF26");
    }

    [Fact]
    public async Task CreateBlocks_ChildrenDoNotAppearAmongThePagesTopLevelBlocks()
    {
        var store = new MockNotionBlockStore();

        await store.CreateBlocksAsync(PageId.ToString(), Table(Guid.NewGuid()), afterBlockId: null);
        var topLevel = (await store.GetBlocksAsync(PageId.ToString())).ToList();

        topLevel.Should().NotContain(block => block.Type == BlockType.TableRow);
    }

    [Fact]
    public async Task CreateBlocks_ChildOfAnUnknownParentKeepsThatParent()
    {
        // The batch may legitimately attach to a block that already exists on the page.
        var store = new MockNotionBlockStore();
        var existingParent = Guid.NewGuid();

        var created = (await store.CreateBlocksAsync(
            PageId.ToString(),
            [Row(existingParent, "orphan", order: 0)],
            afterBlockId: null)).ToList();

        created.Should().ContainSingle().Which.ParentBlockId.Should().Be(existingParent);
    }

    private static List<IPageBlock> Table(Guid tableId) =>
    [
        new PageBlock { Id = tableId, PageId = PageId, Type = BlockType.Table, Order = 0, Content = new TableBlockContent { ColumnCount = 1, HasHeaderRow = true } },
        Row(tableId, "Name", 0),
        Row(tableId, "CF26", 1)
    ];

    private static PageBlock Row(Guid parentId, string cell, int order) => new()
    {
        Id = Guid.NewGuid(),
        PageId = PageId,
        ParentBlockId = parentId,
        Type = BlockType.TableRow,
        Order = order,
        Content = new TableRowBlockContent { Cells = [cell] }
    };
}
