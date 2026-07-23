using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Undo of a delete restores the blocks it removed. They must come back with the ids they had, or a
/// restored container and its children no longer point at each other.
/// </summary>
public sealed class NotionRestoreBlocksTests
{
    private static readonly Guid PageId = Guid.Parse("dddddddd-1111-2222-3333-444444444444");

    [Fact]
    public async Task RestoreBlocks_BringsTheSubtreeBackWithItsOriginalIds()
    {
        var store = new MockNotionBlockStore();
        var table = await store.CreateBlockAsync(PageId.ToString(), Table(), null);
        var row = await store.CreateBlockAsync(PageId.ToString(), Row(table.Id, "cell"), null);

        var snapshot = new List<IPageBlock> { Clone(table), Clone(row) };
        await store.DeleteBlockAsync(table.Id.ToString());

        await store.RestoreBlocksAsync(snapshot);

        var restored = store.GetAllBlocksSnapshot();
        restored.Should().Contain(block => block.Id == table.Id);
        restored.Single(block => block.Id == row.Id).ParentBlockId.Should().Be(table.Id);
        (await store.GetChildBlocksAsync(table.Id.ToString())).Should().ContainSingle();
    }

    [Fact]
    public async Task RestoreBlocks_PutsTheBlockBackAtItsOldPosition()
    {
        var store = new MockNotionBlockStore();
        var first = await store.CreateBlockAsync(PageId.ToString(), Paragraph("one"), null);
        var second = await store.CreateBlockAsync(PageId.ToString(), Paragraph("two"), null);
        var third = await store.CreateBlockAsync(PageId.ToString(), Paragraph("three"), null);

        var snapshot = new List<IPageBlock> { Clone(second) };
        await store.DeleteBlockAsync(second.Id.ToString());
        await store.RestoreBlocksAsync(snapshot);

        var restored = (await store.GetBlocksAsync(PageId.ToString())).ToList();

        // Distinct orders, not just a lucky tie broken by insertion order.
        restored.Select(block => block.Order).Should().OnlyHaveUniqueItems();
        restored.OrderBy(block => block.Order)
            .Select(block => ((ITextBlockContent)block.Content).Html)
            .Should().Equal("one", "two", "three");
    }

    [Fact]
    public async Task RestoreBlocks_OnAnEmptyList_DoesNothing()
    {
        var store = new MockNotionBlockStore();
        var before = store.GetAllBlocksSnapshot().Count;

        await store.RestoreBlocksAsync([]);

        store.GetAllBlocksSnapshot().Should().HaveCount(before);
    }

    [Fact]
    public async Task RestoreBlocks_OverAnExistingBlock_ReplacesIt()
    {
        var store = new MockNotionBlockStore();
        var block = await store.CreateBlockAsync(PageId.ToString(), Paragraph("before"), null);

        await store.RestoreBlocksAsync([Clone(block, "after")]);

        var restored = store.GetAllBlocksSnapshot().Single(b => b.Id == block.Id);
        ((ITextBlockContent)restored.Content).Html.Should().Be("after");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static PageBlock Clone(IPageBlock source, string? html = null) => new()
    {
        Id = source.Id,
        PageId = source.PageId,
        ParentBlockId = source.ParentBlockId,
        Type = source.Type,
        Order = source.Order,
        Content = html is null ? source.Content : new TextBlockContent { Html = html },
        CreatedAt = source.CreatedAt,
        LastEditedAt = source.LastEditedAt
    };

    private static PageBlock Paragraph(string html) =>
        new() { PageId = PageId, Type = BlockType.Paragraph, Content = new TextBlockContent { Html = html } };

    private static PageBlock Table() =>
        new() { PageId = PageId, Type = BlockType.Table, Content = new TableBlockContent { ColumnCount = 1 } };

    private static PageBlock Row(Guid parentId, string cell) => new()
    {
        PageId = PageId, ParentBlockId = parentId, Type = BlockType.TableRow, Order = 0,
        Content = new TableRowBlockContent
        {
            RichCells = [new NotionTableCell { Html = cell }]
        }
    };
}
