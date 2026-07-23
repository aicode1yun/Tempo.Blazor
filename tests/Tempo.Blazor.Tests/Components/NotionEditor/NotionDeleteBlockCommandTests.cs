using FluentAssertions;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Undo of a delete must put back exactly what was removed. Re-creating the block would give it a
/// new id, so a restored table would no longer own the rows that still point at its old one.
/// </summary>
public sealed class NotionDeleteBlockCommandTests
{
    private static readonly Guid PageId = Guid.Parse("f0f0f0f0-1111-2222-3333-444455556666");

    [Fact]
    public async Task Undo_RestoresTheBlockWithItsOriginalId()
    {
        var provider = Substitute.For<INotionBlockProvider>();
        var block = Paragraph("gone", order: 1);
        var blocks = new List<IPageBlock> { Paragraph("before", 0), block, Paragraph("after", 2) };

        var command = new DeleteBlockCommand(provider, blocks, PageId.ToString(), block);
        await command.ExecuteAsync();
        await command.UndoAsync();

        await provider.Received(1).RestoreBlocksAsync(
            Arg.Is<IEnumerable<IPageBlock>>(restored => restored.Single().Id == block.Id));
        await provider.DidNotReceiveWithAnyArgs().CreateBlockAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Undo_RestoresTheWholeSubtree()
    {
        var provider = Substitute.For<INotionBlockProvider>();
        var table = new PageBlock { Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.Table, Order = 0, Content = new TableBlockContent() };
        var rows = new IPageBlock[] { Row(table.Id, 0), Row(table.Id, 1) };
        var blocks = new List<IPageBlock> { table };

        var command = new DeleteBlockCommand(provider, blocks, PageId.ToString(), table, rows);
        await command.ExecuteAsync();
        await command.UndoAsync();

        await provider.Received(1).RestoreBlocksAsync(
            Arg.Is<IEnumerable<IPageBlock>>(restored => restored.Count() == 3));
    }

    [Fact]
    public async Task Execute_RemovesTheBlockFromTheLocalList()
    {
        var provider = Substitute.For<INotionBlockProvider>();
        var block = Paragraph("gone", 1);
        var blocks = new List<IPageBlock> { Paragraph("before", 0), block };

        await new DeleteBlockCommand(provider, blocks, PageId.ToString(), block).ExecuteAsync();

        blocks.Should().ContainSingle();
        await provider.Received(1).DeleteBlockAsync(block.Id.ToString());
    }

    [Fact]
    public async Task Undo_PutsTheBlockBackBetweenItsNeighbours()
    {
        var provider = Substitute.For<INotionBlockProvider>();
        var block = Paragraph("middle", 1);
        var blocks = new List<IPageBlock> { Paragraph("before", 0), block, Paragraph("after", 2) };

        var command = new DeleteBlockCommand(provider, blocks, PageId.ToString(), block);
        await command.ExecuteAsync();
        await command.UndoAsync();

        blocks.Select(b => ((ITextBlockContent)b.Content).Html).Should().Equal("before", "middle", "after");
    }

    [Fact]
    public async Task Undo_DoesNotAddChildrenToThePagesTopLevelList()
    {
        var provider = Substitute.For<INotionBlockProvider>();
        var table = new PageBlock { Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.Table, Order = 0, Content = new TableBlockContent() };
        var blocks = new List<IPageBlock> { table };

        var command = new DeleteBlockCommand(provider, blocks, PageId.ToString(), table, [Row(table.Id, 0)]);
        await command.ExecuteAsync();
        await command.UndoAsync();

        blocks.Should().ContainSingle().Which.Type.Should().Be(BlockType.Table);
    }

    private static PageBlock Paragraph(string html, int order) => new()
    {
        Id = Guid.NewGuid(), PageId = PageId, Type = BlockType.Paragraph, Order = order,
        Content = new TextBlockContent { Html = html }
    };

    private static PageBlock Row(Guid parentId, int order) => new()
    {
        Id = Guid.NewGuid(), PageId = PageId, ParentBlockId = parentId,
        Type = BlockType.TableRow, Order = order,
        Content = new TableRowBlockContent
        {
            RichCells = [new NotionTableCell { Html = "x" }]
        }
    };
}
