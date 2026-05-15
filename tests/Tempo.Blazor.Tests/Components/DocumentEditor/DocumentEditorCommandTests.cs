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

    [Fact]
    public async Task MoveDocumentBlockCommand_UndoRedoPreservesImageMetadata()
    {
        var image = new DocumentBlock
        {
            Id = "img-1",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = "asset-1",
                Url = "/assets/1.png",
                AltText = "Evidence image",
                Size = new DocumentImageSize { Width = 240, Height = 120 }
            }
        };
        var document = CreateDocument(Paragraph("One"), image, Paragraph("Two"));
        var stack = new DocumentEditorCommandStack();

        await stack.PushAsync(new MoveDocumentBlockCommand(document, image.Id, 0));

        document.Blocks.OrderBy(block => block.Order).First().Id.Should().Be("img-1");
        ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content).AssetId.Should().Be("asset-1");

        await stack.UndoAsync();
        document.Blocks.OrderBy(block => block.Order).Select(block => block.Id).Should().ContainInOrder(
            document.Blocks.Single(block => TextOf(block) == "One").Id,
            "img-1",
            document.Blocks.Single(block => TextOf(block) == "Two").Id);

        await stack.RedoAsync();
        var moved = document.Blocks.OrderBy(block => block.Order).First();
        moved.Id.Should().Be("img-1");
        var content = moved.Content.Should().BeOfType<ImageBlockContent>().Subject;
        content.AssetId.Should().Be("asset-1");
        content.AltText.Should().Be("Evidence image");
        content.Size.Width.Should().Be(240);
    }

    [Fact]
    public async Task Commands_AreIdempotentAndExposeUndoMetadata()
    {
        var document = CreateDocument(Paragraph("One"), Paragraph("Two"), Paragraph("Three"));
        var firstId = document.Blocks.OrderBy(block => block.Order).First().Id;
        var second = document.Blocks.OrderBy(block => block.Order).Skip(1).First();
        var move = new MoveDocumentBlockCommand(document, second.Id, 0, "Move smoke block");

        await move.ExecuteAsync();
        var once = document.Blocks.OrderBy(block => block.Order).Select(block => block.Id).ToList();
        await move.ExecuteAsync();
        var twice = document.Blocks.OrderBy(block => block.Order).Select(block => block.Id).ToList();

        twice.Should().Equal(once);
        move.Description.Should().Be("Move smoke block");

        await move.UndoAsync();
        var undone = document.Blocks.OrderBy(block => block.Order).Select(block => block.Id).ToList();
        await move.UndoAsync();

        document.Blocks.OrderBy(block => block.Order).Select(block => block.Id).Should().Equal(undone);
        undone.First().Should().Be(firstId);
    }

    [Fact]
    public async Task SnapshotCommand_UndoRedoPreservesWholeDocumentFormattingMetadata()
    {
        var target = CreateDocument(Paragraph("Before"));
        target.Theme.BodyFontFamily = "Aptos, Arial, sans-serif";
        var before = Clone(target);
        var after = Clone(target);
        after.Theme.BodyFontFamily = "Georgia, serif";
        after.HeadersFooters.Add(new DocumentHeaderFooter { Id = "hf-1", Type = DocumentHeaderFooterType.Header });
        after.Revisions.Add(new DocumentRevision { Id = "rev-1", Type = DocumentRevisionType.Formatting });
        var command = new DocumentEditorSnapshotCommand(target, before, after, "Apply review snapshot");

        await command.ExecuteAsync();

        target.Theme.BodyFontFamily.Should().Be("Georgia, serif");
        target.HeadersFooters.Should().ContainSingle(headerFooter => headerFooter.Id == "hf-1");
        target.Revisions.Should().ContainSingle(revision => revision.Id == "rev-1");
        command.Description.Should().Be("Apply review snapshot");

        await command.UndoAsync();

        target.Theme.BodyFontFamily.Should().Be("Aptos, Arial, sans-serif");
        target.HeadersFooters.Should().BeEmpty();
        target.Revisions.Should().BeEmpty();
    }

    [Fact]
    public async Task CommandStack_DescriptionsTrackUndoRedoCommands()
    {
        var document = CreateDocument(Paragraph("Start"));
        var stack = new DocumentEditorCommandStack();

        await stack.PushAsync(new UpdateDocumentBlockCommand(
            document,
            document.Blocks[0].Id,
            document.Blocks[0].Content,
            new ParagraphBlockContent { Inlines = [new TextRun { Text = "Changed" }] },
            "Typing smoke"));

        stack.NextUndoDescription.Should().Be("Typing smoke");
        stack.NextRedoDescription.Should().BeNull();

        await stack.UndoAsync();

        stack.NextUndoDescription.Should().BeNull();
        stack.NextRedoDescription.Should().Be("Typing smoke");
    }

    [Fact]
    public async Task CommandStack_BatchCollectsMultipleCommandsAsOneUndo()
    {
        var document = CreateDocument(Paragraph("Start"));
        var stack = new DocumentEditorCommandStack();

        stack.BeginBatch("Typing");
        await stack.PushAsync(new UpdateDocumentBlockCommand(
            document,
            document.Blocks[0].Id,
            document.Blocks[0].Content,
            new ParagraphBlockContent { Inlines = [new TextRun { Text = "A" }] }));
        await stack.PushAsync(new UpdateDocumentBlockCommand(
            document,
            document.Blocks[0].Id,
            document.Blocks[0].Content,
            new ParagraphBlockContent { Inlines = [new TextRun { Text = "AB" }] }));
        stack.CommitBatch();

        stack.CanUndo.Should().BeTrue();
        stack.CanRedo.Should().BeFalse();

        await stack.UndoAsync();
        TextOf(document.Blocks[0]).Should().Be("Start");
        stack.CanRedo.Should().BeTrue();

        await stack.RedoAsync();
        TextOf(document.Blocks[0]).Should().Be("AB");
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

    private static DocumentEditorDocument Clone(DocumentEditorDocument document)
        => DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(document));
}
