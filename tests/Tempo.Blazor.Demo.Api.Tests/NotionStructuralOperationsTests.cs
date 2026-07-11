using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Structural operations must keep the block tree consistent: a block inserted in the middle stays
/// in the middle after a reload, deleting a container takes its subtree with it, and duplicating a
/// container produces an independent copy rather than one that shares the original's content.
/// </summary>
public sealed class NotionStructuralOperationsTests
{
    private static readonly Guid PageId = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid OtherPageId = Guid.Parse("99998888-7777-6666-5555-444433332222");

    // ── Insert order ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBlock_AfterAGivenBlock_LandsRightBehindIt()
    {
        var store = new MockNotionBlockStore();
        var (first, _, _) = await ThreeParagraphsAsync(store);

        await store.CreateBlockAsync(PageId.ToString(), Paragraph("inserted"), first.Id.ToString());

        var order = await TopLevelTextAsync(store);
        order.Should().Equal("one", "inserted", "two", "three");
    }

    [Fact]
    public async Task CreateBlock_WithoutAnAnchor_GoesToTheEnd()
    {
        var store = new MockNotionBlockStore();
        await ThreeParagraphsAsync(store);

        await store.CreateBlockAsync(PageId.ToString(), Paragraph("last"), afterBlockId: null);

        (await TopLevelTextAsync(store)).Should().Equal("one", "two", "three", "last");
    }

    [Fact]
    public async Task CreateBlocks_AfterAGivenBlock_KeepTheirOwnOrderInTheMiddle()
    {
        var store = new MockNotionBlockStore();
        var (first, _, _) = await ThreeParagraphsAsync(store);

        await store.CreateBlocksAsync(
            PageId.ToString(),
            [Paragraph("a"), Paragraph("b")],
            first.Id.ToString());

        (await TopLevelTextAsync(store)).Should().Equal("one", "a", "b", "two", "three");
    }

    [Fact]
    public async Task CreateBlock_InsideAContainer_DoesNotDisturbTheTopLevelOrder()
    {
        var store = new MockNotionBlockStore();
        var (first, _, _) = await ThreeParagraphsAsync(store);
        var toggle = await store.CreateBlockAsync(PageId.ToString(), Toggle(), first.Id.ToString());

        var child = Paragraph("inside");
        child.ParentBlockId = toggle.Id;
        await store.CreateBlockAsync(PageId.ToString(), child, afterBlockId: null);

        (await TopLevelTextAsync(store)).Should().Equal("one", string.Empty, "two", "three");
        (await store.GetChildBlocksAsync(toggle.Id.ToString())).Should().ContainSingle();
    }

    // ── Cascading delete ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBlock_TakesItsWholeSubtreeWithIt()
    {
        var store = new MockNotionBlockStore();
        var toggle = await store.CreateBlockAsync(PageId.ToString(), Toggle(), null);
        var child = await CreateChildAsync(store, toggle.Id, "child");
        var grandchild = await CreateChildAsync(store, child.Id, "grandchild");

        await store.DeleteBlockAsync(toggle.Id.ToString());

        var all = store.GetAllBlocksSnapshot();
        all.Should().NotContain(block => block.Id == toggle.Id);
        all.Should().NotContain(block => block.Id == child.Id, "an orphaned child would render as a stray block");
        all.Should().NotContain(block => block.Id == grandchild.Id);
    }

    [Fact]
    public async Task DeleteBlock_LeavesSiblingsAlone()
    {
        var store = new MockNotionBlockStore();
        var toggle = await store.CreateBlockAsync(PageId.ToString(), Toggle(), null);
        await CreateChildAsync(store, toggle.Id, "child");
        var sibling = await store.CreateBlockAsync(PageId.ToString(), Paragraph("sibling"), null);

        await store.DeleteBlockAsync(toggle.Id.ToString());

        store.GetAllBlocksSnapshot().Should().Contain(block => block.Id == sibling.Id);
    }

    // ── Duplication ───────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicateBlock_CopiesTheWholeSubtree()
    {
        var store = new MockNotionBlockStore();
        var table = await store.CreateBlockAsync(PageId.ToString(), Table(), null);
        await CreateRowAsync(store, table.Id, "a", 0);
        await CreateRowAsync(store, table.Id, "b", 1);

        var copy = await store.DuplicateBlockAsync(table.Id.ToString());

        var rows = (await store.GetChildBlocksAsync(copy.Id.ToString())).ToList();
        rows.Should().HaveCount(2, "a duplicated table without its rows renders empty");
        rows.Select(row => ((TableRowBlockContent)row.Content).Cells[0]).Should().Equal("a", "b");
        rows.Should().OnlyContain(row => row.ParentBlockId == copy.Id);
    }

    [Fact]
    public async Task DuplicateBlock_DoesNotShareContentWithTheOriginal()
    {
        var store = new MockNotionBlockStore();
        var source = await store.CreateBlockAsync(PageId.ToString(), Paragraph("original"), null);

        var copy = await store.DuplicateBlockAsync(source.Id.ToString());
        ((TextBlockContent)copy.Content).Html = "edited";

        ((TextBlockContent)source.Content).Html.Should().Be("original",
            "the copy must not share the original's content instance");
    }

    [Fact]
    public async Task DuplicateBlock_LandsRightAfterTheOriginal()
    {
        var store = new MockNotionBlockStore();
        var (first, _, _) = await ThreeParagraphsAsync(store);

        await store.DuplicateBlockAsync(first.Id.ToString());

        (await TopLevelTextAsync(store)).Should().Equal("one", "one", "two", "three");
    }

    // ── Move across pages ─────────────────────────────────────────────────

    [Fact]
    public async Task MoveBlock_ToAnotherPage_MovesItsDescendantsToo()
    {
        var store = new MockNotionBlockStore();
        var toggle = await store.CreateBlockAsync(PageId.ToString(), Toggle(), null);
        var child = await CreateChildAsync(store, toggle.Id, "child");
        var grandchild = await CreateChildAsync(store, child.Id, "grandchild");

        await store.MoveBlockAsync(new MoveNotionBlockRequest(
            toggle.Id.ToString(), OtherPageId.ToString(), null, null, 0));

        var all = store.GetAllBlocksSnapshot();
        all.Single(block => block.Id == toggle.Id).PageId.Should().Be(OtherPageId);
        all.Single(block => block.Id == child.Id).PageId.Should().Be(OtherPageId,
            "a child left behind on the old page is unreachable");
        all.Single(block => block.Id == grandchild.Id).PageId.Should().Be(OtherPageId);
    }

    [Fact]
    public async Task MoveBlock_UsesTheStoredParent_NotTheRequestedOne()
    {
        var store = new MockNotionBlockStore();
        var toggle = await store.CreateBlockAsync(PageId.ToString(), Toggle(), null);
        var child = await CreateChildAsync(store, toggle.Id, "child");
        var sibling = await CreateChildAsync(store, toggle.Id, "sibling");

        // The caller lies about the source parent; the store must not trust it.
        await store.MoveBlockAsync(new MoveNotionBlockRequest(
            child.Id.ToString(), PageId.ToString(), Guid.NewGuid().ToString(), null, 0));

        var moved = store.GetAllBlocksSnapshot().Single(block => block.Id == child.Id);
        moved.ParentBlockId.Should().BeNull();

        var remaining = (await store.GetChildBlocksAsync(toggle.Id.ToString())).ToList();
        remaining.Should().ContainSingle().Which.Id.Should().Be(sibling.Id);
        remaining[0].Order.Should().Be(0, "the surviving sibling must be renumbered from zero");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task<(IPageBlock First, IPageBlock Second, IPageBlock Third)> ThreeParagraphsAsync(
        MockNotionBlockStore store)
    {
        var first = await store.CreateBlockAsync(PageId.ToString(), Paragraph("one"), null);
        var second = await store.CreateBlockAsync(PageId.ToString(), Paragraph("two"), null);
        var third = await store.CreateBlockAsync(PageId.ToString(), Paragraph("three"), null);
        return (first, second, third);
    }

    private static async Task<IPageBlock> CreateChildAsync(MockNotionBlockStore store, Guid parentId, string html)
    {
        var block = Paragraph(html);
        block.ParentBlockId = parentId;
        return await store.CreateBlockAsync(PageId.ToString(), block, null);
    }

    private static async Task<IPageBlock> CreateRowAsync(MockNotionBlockStore store, Guid tableId, string cell, int order)
    {
        var row = new PageBlock
        {
            PageId = PageId,
            ParentBlockId = tableId,
            Type = BlockType.TableRow,
            Order = order,
            Content = new TableRowBlockContent { Cells = [cell] }
        };
        return await store.CreateBlockAsync(PageId.ToString(), row, null);
    }

    private static async Task<List<string>> TopLevelTextAsync(MockNotionBlockStore store) =>
        (await store.GetBlocksAsync(PageId.ToString()))
            .OrderBy(block => block.Order)
            .Select(block => (block.Content as ITextBlockContent)?.Html ?? string.Empty)
            .ToList();

    private static PageBlock Paragraph(string html) => new()
    {
        PageId = PageId,
        Type = BlockType.Paragraph,
        Content = new TextBlockContent { Html = html }
    };

    private static PageBlock Toggle() => new()
    {
        PageId = PageId,
        Type = BlockType.Toggle,
        Content = new ToggleBlockContent { Html = string.Empty, IsOpen = true }
    };

    private static PageBlock Table() => new()
    {
        PageId = PageId,
        Type = BlockType.Table,
        Content = new TableBlockContent { ColumnCount = 1 }
    };
}
