using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Tempo.Blazor.Configuration;
using Tempo.Blazor.Components.NotionEditor.Blocks;
using Tempo.Blazor.Components.NotionEditor.Page;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Backspace at the start of a block folds it into the one above: the predecessor absorbs the
/// text, the block disappears, and neither half is lost. The predecessor keeps its own type,
/// so merging a paragraph into a heading leaves a heading.
/// </summary>
public sealed class TmNotionPageMergeTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task MergeBlockIntoPrevious_AppendsTheHtmlAndDeletesTheBlock()
    {
        var first  = Paragraph("alpha", order: 0);
        var second = Paragraph("beta", order: 1);
        var (cut, provider) = RenderPage(first, second);

        await cut.InvokeAsync(() => cut.Instance.MergeBlockIntoPreviousAsync(second.Id.ToString(), "beta"));

        await provider.Received(1).DeleteBlockAsync(second.Id.ToString());
        await provider.Received(1).UpdateBlockAsync(Arg.Is<IPageBlock>(block =>
            block.Id == first.Id && ((ITextBlockContent)block.Content).Html == "alphabeta"));

        cut.Instance.Blocks.Should().ContainSingle();
        cut.Instance.Blocks[0].Id.Should().Be(first.Id);
        ((ITextBlockContent)cut.Instance.Blocks[0].Content).Html.Should().Be("alphabeta");
    }

    [Fact]
    public async Task MergeBlockIntoPrevious_KeepsThePredecessorsBlockType()
    {
        var heading = new PageBlock
        {
            Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.Heading1, Order = 0,
            Content = new HeadingBlockContent { Html = "Title", Level = 1 }
        };
        var paragraph = Paragraph("tail", order: 1);
        var (cut, _) = RenderPage(heading, paragraph);

        await cut.InvokeAsync(() => cut.Instance.MergeBlockIntoPreviousAsync(paragraph.Id.ToString(), "tail"));

        var merged = cut.Instance.Blocks.Should().ContainSingle().Subject;
        merged.Type.Should().Be(BlockType.Heading1);
        merged.Content.Should().BeOfType<HeadingBlockContent>()
            .Which.Html.Should().Be("Titletail");
    }

    [Fact]
    public async Task MergeBlockIntoPrevious_OnTheFirstBlock_ChangesNothing()
    {
        var first  = Paragraph("alpha", order: 0);
        var second = Paragraph("beta", order: 1);
        var (cut, provider) = RenderPage(first, second);

        await cut.InvokeAsync(() => cut.Instance.MergeBlockIntoPreviousAsync(first.Id.ToString(), "alpha"));

        await provider.DidNotReceiveWithAnyArgs().DeleteBlockAsync(default!);
        await provider.DidNotReceiveWithAnyArgs().UpdateBlockAsync(default!);
        cut.Instance.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeBlockIntoPrevious_WhenReadOnly_ChangesNothing()
    {
        var first  = Paragraph("alpha", order: 0);
        var second = Paragraph("beta", order: 1);
        var (cut, provider) = RenderPage(readOnly: true, first, second);

        await cut.InvokeAsync(() => cut.Instance.MergeBlockIntoPreviousAsync(second.Id.ToString(), "beta"));

        await provider.DidNotReceiveWithAnyArgs().DeleteBlockAsync(default!);
        cut.Instance.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeBlockIntoPrevious_WhenTheProviderFails_KeepsBothBlocks()
    {
        var first  = Paragraph("alpha", order: 0);
        var second = Paragraph("beta", order: 1);
        var (cut, provider) = RenderPage(first, second);
        provider.UpdateBlockAsync(Arg.Any<IPageBlock>()).Returns(_ => throw new InvalidOperationException("offline"));

        await cut.InvokeAsync(() => cut.Instance.MergeBlockIntoPreviousAsync(second.Id.ToString(), "beta"));

        cut.Instance.Blocks.Should().HaveCount(2, "a failed save must not drop the block locally");
        await provider.DidNotReceiveWithAnyArgs().DeleteBlockAsync(default!);
    }

    [Fact]
    public async Task DeleteBlock_MovesTheCaretHomeToThePreviousBlock()
    {
        var first  = Paragraph("alpha", order: 0);
        var second = Paragraph("", order: 1);
        var (cut, _) = RenderPage(first, second);

        await cut.InvokeAsync(() => cut.Instance.SetActiveBlock(second.Id));
        await cut.InvokeAsync(() => cut.Instance.DeleteBlockAsync(second.Id.ToString()));

        cut.Instance.ActiveBlockId.Should().Be(first.Id,
            "Backspace on an empty block must leave the caret in the block above, not nowhere");
    }

    [Fact]
    public async Task DeleteBlock_OfTheOnlyBlock_LeavesNoActiveBlock()
    {
        var only = Paragraph("alpha", order: 0);
        var (cut, _) = RenderPage(only);

        await cut.InvokeAsync(() => cut.Instance.SetActiveBlock(only.Id));
        await cut.InvokeAsync(() => cut.Instance.DeleteBlockAsync(only.Id.ToString()));

        cut.Instance.ActiveBlockId.Should().BeNull();
    }

    [Fact]
    public async Task InsertingBlocksWithChildren_ReparentsThemOntoTheNewIds()
    {
        // A pasted table arrives as a Table parent plus TableRow children. The insert path assigns
        // fresh ids, so the children must be re-pointed at the parent's new id — otherwise the
        // table renders empty and the orphaned rows fall back to paragraphs reading "TableRow".
        var anchor = Paragraph("anchor", order: 0);
        var (cut, provider) = RenderPage(anchor);

        var tableId = Guid.NewGuid();
        var pasted = new List<IPageBlock>
        {
            new PageBlock { Id = tableId, PageId = PageId, Type = BlockType.Table, Order = 0, Content = new TableBlockContent { ColumnCount = 1 } },
            new PageBlock { Id = Guid.NewGuid(), PageId = PageId, ParentBlockId = tableId, Type = BlockType.TableRow, Order = 0, Content = new TableRowBlockContent { Cells = ["a"] } },
            new PageBlock { Id = Guid.NewGuid(), PageId = PageId, ParentBlockId = tableId, Type = BlockType.TableRow, Order = 1, Content = new TableRowBlockContent { Cells = ["b"] } }
        };

        provider.CreateBlocksAsync(Arg.Any<string>(), Arg.Any<IEnumerable<IPageBlock>>(), Arg.Any<string?>())
            .Returns(callInfo => Task.FromResult(callInfo.ArgAt<IEnumerable<IPageBlock>>(1)));

        var list = cut.FindComponent<TmNotionBlockList>();
        await cut.InvokeAsync(() => list.Instance.OnInsertTemplateBlocksAfter
            .InvokeAsync((anchor.Id.ToString(), (IReadOnlyList<IPageBlock>)pasted)));

        var sent = provider.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(INotionBlockProvider.CreateBlocksAsync))
            .GetArguments()[1] as IEnumerable<IPageBlock>;

        var persisted = sent!.ToList();
        var table = persisted.Single(block => block.Type == BlockType.Table);
        var rows = persisted.Where(block => block.Type == BlockType.TableRow).ToList();

        table.Id.Should().NotBe(tableId, "the insert path assigns fresh ids");
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.ParentBlockId == table.Id);

        // Only the top level lands in the page's block list; the table fetches its own rows.
        cut.Instance.Blocks.Should().NotContain(block => block.Type == BlockType.TableRow);
        cut.Instance.Blocks.Should().Contain(block => block.Type == BlockType.Table);
    }

    [Fact]
    public async Task DeletingABlock_CanBeUndone()
    {
        var first = Paragraph("keep", order: 0);
        var second = Paragraph("gone", order: 1);
        var (cut, provider) = RenderPage(first, second);
        provider.GetChildBlocksAsync(Arg.Any<string>()).Returns([]);

        await cut.InvokeAsync(() => cut.Instance.DeleteBlockAsync(second.Id.ToString()));
        cut.Instance.Blocks.Should().ContainSingle();
        cut.Instance.CanUndo.Should().BeTrue();

        // The page reloads from the provider after an undo.
        provider.GetBlocksAsync(PageId.ToString()).Returns([first, second]);
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        await provider.Received(1).RestoreBlocksAsync(
            Arg.Is<IEnumerable<IPageBlock>>(restored => restored.Single().Id == second.Id));
        cut.Instance.Blocks.Should().HaveCount(2);
        cut.Instance.CanRedo.Should().BeTrue();
    }

    [Fact]
    public async Task DeletingAContainer_RestoresItsChildrenOnUndo()
    {
        var table = new PageBlock
        {
            Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.Table, Order = 0,
            Content = new TableBlockContent { ColumnCount = 1 }
        };
        var row = new PageBlock
        {
            Id = Guid.NewGuid(), PageId = PageId, ParentBlockId = table.Id,
            Type = BlockType.TableRow, Order = 0, Content = new TableRowBlockContent { Cells = ["a"] }
        };

        var (cut, provider) = RenderPage(table);
        provider.GetChildBlocksAsync(table.Id.ToString()).Returns([row]);
        provider.GetChildBlocksAsync(row.Id.ToString()).Returns([]);

        await cut.InvokeAsync(() => cut.Instance.DeleteBlockAsync(table.Id.ToString()));
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        await provider.Received(1).RestoreBlocksAsync(
            Arg.Is<IEnumerable<IPageBlock>>(restored => restored.Count() == 2
                && restored.Any(block => block.Id == row.Id)));
    }

    [Fact]
    public async Task UndoOnAReadOnlyPage_DoesNothing()
    {
        var block = Paragraph("x", 0);
        var (cut, provider) = RenderPage(readOnly: true, block);

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        cut.Instance.CanUndo.Should().BeFalse();
        await provider.DidNotReceiveWithAnyArgs().RestoreBlocksAsync(default!);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private (IRenderedComponent<TmNotionPage> Cut, INotionBlockProvider Provider) RenderPage(
        params IPageBlock[] blocks) => RenderPage(readOnly: false, blocks);

    private (IRenderedComponent<TmNotionPage> Cut, INotionBlockProvider Provider) RenderPage(
        bool readOnly, params IPageBlock[] blocks)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddTempoBlazorNotionEditor(); // TmNotionPage's comment panel injects orchestrator services

        var provider = Substitute.For<INotionBlockProvider>();
        provider.GetBlocksAsync(PageId.ToString()).Returns(blocks);

        var context = new NotionEditorContext { BlockProvider = provider };
        var page    = new NotionPage { Id = PageId, Title = "Page" };

        var cut = RenderComponent<TmNotionPage>(parameters => parameters
            .AddCascadingValue(context)
            .Add(p => p.Page, page)
            .Add(p => p.ReadOnly, readOnly));

        return (cut, provider);
    }

    private static PageBlock Paragraph(string html, int order) => new()
    {
        Id      = Guid.NewGuid(),
        PageId  = PageId,
        Type    = BlockType.Paragraph,
        Order   = order,
        Content = new TextBlockContent { Html = html }
    };
}
