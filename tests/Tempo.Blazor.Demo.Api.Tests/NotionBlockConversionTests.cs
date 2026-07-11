using FluentAssertions;
using Tempo.Blazor.Demo.Api.Data;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Demo.Api.Tests;

/// <summary>
/// Converting a block must never silently drop its text, its typed data, or its children.
/// </summary>
public sealed class NotionBlockConversionTests
{
    private static readonly Guid PageId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    // ── (a) live HTML wins over the stored content ───────────────────────────

    [Fact]
    public async Task Convert_UsesLiveHtmlWhenSupplied()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Heading1, new HeadingBlockContent { Level = 1, Html = "stale" });

        var converted = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Heading2, "typed but not saved");

        converted.Type.Should().Be(BlockType.Heading2);
        Html(converted).Should().Be("typed but not saved");
    }

    [Fact]
    public async Task Convert_WithoutLiveHtmlKeepsStoredContent()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Heading1, new HeadingBlockContent { Level = 1, Html = "saved" });

        var converted = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Heading2);

        Html(converted).Should().Be("saved");
    }

    // ── (b) Paragraph ↔ Code ────────────────────────────────────────────────

    [Fact]
    public async Task Convert_ParagraphToCodeKeepsText()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Paragraph, new TextBlockContent { Html = "hello" });

        var converted = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Code);

        converted.Content.Should().BeAssignableTo<ICodeBlockContent>()
            .Which.Code.Should().Be("hello");
    }

    [Fact]
    public async Task Convert_CodeToParagraphKeepsText()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Code, new CodeBlockContent { Code = "var x = 1;", Language = "C#" });

        var converted = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Paragraph);

        Html(converted).Should().Be("var x = 1;");
    }

    [Fact]
    public async Task Convert_CodeRoundTripKeepsLanguage()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Code, new CodeBlockContent { Code = "print(1)", Language = "Python" });

        await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Paragraph);
        var back = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Code);

        var code = back.Content.Should().BeAssignableTo<ICodeBlockContent>().Subject;
        code.Code.Should().Be("print(1)");
        code.Language.Should().Be("Python");
    }

    // ── (c) container → non-container must not orphan children ──────────────

    [Fact]
    public async Task Convert_ToggleToParagraphReparentsChildren()
    {
        var store = new MockNotionBlockStore();
        var toggle = await AddBlockAsync(store, BlockType.Toggle, new ToggleBlockContent { Html = "parent" });
        var child = await AddChildAsync(store, toggle.Id, BlockType.Paragraph, new TextBlockContent { Html = "child" });

        await store.ConvertBlockTypeAsync(toggle.Id.ToString(), BlockType.Paragraph);

        var stored = store.GetAllBlocksSnapshot().Single(b => b.Id == child.Id);
        stored.ParentBlockId.Should().Be(toggle.ParentBlockId, "the child must move up to the toggle's own parent, not dangle");
        (await store.GetChildBlocksAsync(toggle.Id.ToString())).Should().BeEmpty();
    }

    [Fact]
    public async Task Convert_FromTableDeletesItsRows()
    {
        var store = new MockNotionBlockStore();
        var table = await AddBlockAsync(store, BlockType.Table, new TableBlockContent { ColumnCount = 2, HasHeaderRow = true });
        var row = await AddChildAsync(store, table.Id, BlockType.TableRow, new TableRowBlockContent { Cells = ["a", "b"] });

        await store.ConvertBlockTypeAsync(table.Id.ToString(), BlockType.Paragraph);

        store.GetAllBlocksSnapshot().Should().NotContain(b => b.Id == row.Id, "table rows are meaningless once the table is gone");
    }

    // ── (d) Callout keeps or reconstructs its icon ──────────────────────────

    [Fact]
    public async Task Convert_ToCalloutGetsDefaultIcon()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Paragraph, new TextBlockContent { Html = "note" });

        var converted = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Callout);

        var callout = converted.Content.Should().BeAssignableTo<ICalloutBlockContent>().Subject;
        callout.IconEmoji.Should().Be("💡");
        callout.Html.Should().Be("note");
    }

    [Fact]
    public async Task Convert_CalloutRoundTripKeepsIconAndVariant()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Callout, new CalloutBlockContent
        {
            Html = "warn",
            IconEmoji = "🎉",
            Variant = CalloutVariant.Warning
        });

        await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Paragraph);
        var back = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Callout);

        var callout = back.Content.Should().BeAssignableTo<ICalloutBlockContent>().Subject;
        callout.IconEmoji.Should().Be("🎉");
        callout.Variant.Should().Be(CalloutVariant.Warning);
        callout.Html.Should().Be("warn");
    }

    // ── (e) Todo keeps its checked state across a round-trip ────────────────

    [Fact]
    public async Task Convert_TodoRoundTripKeepsCheckedState()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.TodoItem, new TodoBlockContent { Html = "done", IsChecked = true });

        await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Paragraph);
        var back = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.TodoItem);

        var todo = back.Content.Should().BeAssignableTo<ITodoBlockContent>().Subject;
        todo.IsChecked.Should().BeTrue();
        todo.Html.Should().Be("done");
    }

    [Fact]
    public async Task Convert_ToTodoWithoutHistoryIsUnchecked()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Paragraph, new TextBlockContent { Html = "task" });

        var converted = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.TodoItem);

        converted.Content.Should().BeAssignableTo<ITodoBlockContent>().Which.IsChecked.Should().BeFalse();
    }

    // ── (f) Paragraph → Table ───────────────────────────────────────────────

    [Fact]
    public async Task Convert_ParagraphToTablePutsTextInFirstCell()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Paragraph, new TextBlockContent { Html = "header" });

        var table = await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Table);

        var rows = (await store.GetChildBlocksAsync(table.Id.ToString())).ToList();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.ParentBlockId == table.Id);
        ((ITableRowBlockContent)rows[0].Content).Cells[0].Should().Be("header");
    }

    [Fact]
    public async Task Convert_ToTableTwiceDoesNotAccumulateRows()
    {
        var store = new MockNotionBlockStore();
        var block = await AddBlockAsync(store, BlockType.Paragraph, new TextBlockContent { Html = "x" });

        await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Table);
        await store.ConvertBlockTypeAsync(block.Id.ToString(), BlockType.Table);

        (await store.GetChildBlocksAsync(block.Id.ToString())).Should().HaveCount(2);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string Html(IPageBlock block)
        => block.Content is ITextBlockContent text ? text.Html : string.Empty;

    private static async Task<IPageBlock> AddBlockAsync(MockNotionBlockStore store, BlockType type, IBlockContent content)
    {
        var block = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = PageId,
            Type = type,
            Order = 0,
            Content = content
        };

        return await store.CreateBlockAsync(PageId.ToString(), block, afterBlockId: null);
    }

    private static async Task<IPageBlock> AddChildAsync(
        MockNotionBlockStore store,
        Guid parentId,
        BlockType type,
        IBlockContent content)
    {
        var block = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = PageId,
            ParentBlockId = parentId,
            Type = type,
            Order = 0,
            Content = content
        };

        return await store.CreateBlockAsync(PageId.ToString(), block, afterBlockId: null);
    }
}
