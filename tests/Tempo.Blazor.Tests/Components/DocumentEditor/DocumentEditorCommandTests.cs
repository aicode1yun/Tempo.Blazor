using FluentAssertions;
using Tempo.Blazor.Components.DocumentEditor.Commands;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Components.DocumentEditor;

public class DocumentEditorCommandTests
{
    [Fact]
    public async Task CommandStack_UndoAndRedoReplaysLatestCommand()
    {
        var document = CreateDocument(Paragraph("Alpha"));
        var stack = new DocumentEditorCommandStack();
        var before = ((ParagraphBlockContent)document.Blocks[0].Content);
        var after = new ParagraphBlockContent { Inlines = [new TextRun { Text = "Beta" }] };

        await stack.PushAsync(new UpdateDocumentBlockCommand(document, document.Blocks[0].Id, before, after));

        TextOf(document.Blocks[0]).Should().Be("Beta");
        stack.CanUndo.Should().BeTrue();

        await stack.UndoAsync();
        TextOf(document.Blocks[0]).Should().Be("Alpha");
        stack.CanRedo.Should().BeTrue();

        await stack.RedoAsync();
        TextOf(document.Blocks[0]).Should().Be("Beta");
    }

    [Fact]
    public async Task InsertDeleteMoveAndBatchCommandsMutateBlocksAsUndoableSteps()
    {
        var document = CreateDocument(Paragraph("One"), Paragraph("Two"));
        var stack = new DocumentEditorCommandStack();
        var inserted = Paragraph("Inserted");

        await stack.PushAsync(new InsertDocumentBlockCommand(document, inserted, document.Blocks[0].Id));
        document.Blocks.OrderBy(block => block.Order).Select(TextOf).Should().ContainInOrder("One", "Inserted", "Two");

        await stack.PushAsync(new MoveDocumentBlockCommand(document, inserted.Id, 0));
        document.Blocks.OrderBy(block => block.Order).Select(TextOf).Should().StartWith("Inserted");

        await stack.PushAsync(new DeleteDocumentBlockCommand(document, inserted.Id));
        document.Blocks.Select(TextOf).Should().NotContain("Inserted");

        await stack.UndoAsync();
        document.Blocks.Select(TextOf).Should().Contain("Inserted");

        stack.BeginBatch("Batch update");
        await stack.PushAsync(new UpdateDocumentBlockCommand(
            document,
            document.Blocks[0].Id,
            document.Blocks[0].Content,
            new ParagraphBlockContent { Inlines = [new TextRun { Text = "Batch A" }] }));
        await stack.PushAsync(new UpdateDocumentBlockCommand(
            document,
            document.Blocks[1].Id,
            document.Blocks[1].Content,
            new ParagraphBlockContent { Inlines = [new TextRun { Text = "Batch B" }] }));
        stack.CommitBatch();

        document.Blocks.Select(TextOf).Should().Contain(["Batch A", "Batch B"]);
        await stack.UndoAsync();
        document.Blocks.Select(TextOf).Should().NotContain(["Batch A", "Batch B"]);
    }

    private static DocumentEditorDocument CreateDocument(params DocumentBlock[] blocks)
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks.AddRange(blocks.Select((block, index) =>
        {
            block.Order = (index + 1) * 10;
            return block;
        }));
        return document;
    }

    private static DocumentBlock Paragraph(string text) => new()
    {
        Type = DocumentBlockType.Paragraph,
        Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] }
    };

    private static string TextOf(DocumentBlock block)
    {
        return block.Content is ParagraphBlockContent paragraph
            ? string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text))
            : string.Empty;
    }
}
