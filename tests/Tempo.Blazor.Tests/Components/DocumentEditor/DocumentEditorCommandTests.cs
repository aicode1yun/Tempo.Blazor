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
    public async Task MoveImageObjectCommand_UpdatesOnlyPositionAndPreservesAnchorMetadata()
    {
        var image = new DocumentBlock
        {
            Id = "img-1",
            Type = DocumentBlockType.Image,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = "asset-1",
                AltText = "Evidence",
                Layout = new DocumentObjectLayout
                {
                    Kind = DocumentObjectLayoutKind.Fixed,
                    Anchor = new DocumentObjectAnchor
                    {
                        BlockId = "anchor-1",
                        MoveWithText = false,
                        FixedOnPage = true,
                        LockAnchor = true
                    },
                    Position = new DocumentObjectPosition
                    {
                        HorizontalRelativeTo = DocumentRelativePosition.Page,
                        VerticalRelativeTo = DocumentRelativePosition.Page,
                        X = 12,
                        Y = 18,
                        HorizontalAlignment = DocumentImageHorizontalPosition.Left
                    },
                    Wrap = new DocumentObjectWrap
                    {
                        Mode = DocumentWrapMode.Square,
                        DistanceRight = 12,
                        DistanceBottom = 8
                    },
                    Transform = new DocumentObjectTransform
                    {
                        Width = 220,
                        Height = 124,
                        LockAspectRatio = true
                    },
                    Stacking = new DocumentObjectStacking
                    {
                        ZIndex = 4,
                        AllowOverlap = true
                    }
                }
            }
        };
        var document = CreateDocument(Paragraph("Before"), image, Paragraph("After"));
        var stack = new DocumentEditorCommandStack();

        var command = new MoveImageObjectCommand(
            document,
            "img-1",
            new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Page,
                X = 12,
                Y = 18,
                HorizontalAlignment = DocumentImageHorizontalPosition.Left
            },
            new DocumentObjectPosition
            {
                HorizontalRelativeTo = DocumentRelativePosition.Page,
                VerticalRelativeTo = DocumentRelativePosition.Page,
                X = 144,
                Y = 96,
                HorizontalAlignment = DocumentImageHorizontalPosition.Left
            });

        await stack.PushAsync(command);

        var moved = ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content).Layout;
        moved.Position.X.Should().Be(144);
        moved.Position.Y.Should().Be(96);
        moved.Anchor.BlockId.Should().Be("anchor-1");
        moved.Anchor.MoveWithText.Should().BeFalse();
        moved.Anchor.FixedOnPage.Should().BeTrue();
        moved.Anchor.LockAnchor.Should().BeTrue();
        moved.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        moved.Transform.Width.Should().Be(220);
        moved.Stacking.ZIndex.Should().Be(4);
        command.InvalidatesLayout.Should().BeTrue();
        command.InvalidatedBlockIds.Should().ContainSingle().Which.Should().Be("img-1");
        command.Description.Should().Be("Move image");

        await stack.UndoAsync();

        var restored = ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content).Layout;
        restored.Position.X.Should().Be(12);
        restored.Position.Y.Should().Be(18);
        restored.Anchor.FixedOnPage.Should().BeTrue();

        await stack.RedoAsync();

        var redone = ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content).Layout;
        redone.Position.X.Should().Be(144);
        redone.Position.Y.Should().Be(96);
        stack.NextUndoDescription.Should().Be("Move image");
    }

    [Fact]
    public async Task ResizeImageObjectCommand_CornerResizePreservesAspectAndUndoMetadata()
    {
        var image = ImageBlock("img-1", width: 220, height: 124);
        var document = CreateDocument(Paragraph("Before"), image, Paragraph("After"));
        var stack = new DocumentEditorCommandStack();

        var command = new ResizeImageObjectCommand(
            document,
            "img-1",
            new DocumentObjectTransform { Width = 220, Height = 124, LockAspectRatio = true },
            new DocumentObjectTransform { Width = 360, LockAspectRatio = true },
            new DocumentObjectPosition { X = 12, Y = 18 },
            new DocumentObjectPosition { X = 8, Y = 14 },
            new ResizeImageObjectConstraints { PreserveAspectRatio = true },
            "Resize evidence image");

        await stack.PushAsync(command);

        var resized = ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content);
        resized.Layout.Transform.Width.Should().Be(360);
        resized.Layout.Transform.Height.Should().BeApproximately(202.91, 0.01);
        resized.Size.Width.Should().Be(360);
        resized.Size.Height.Should().BeApproximately(202.91, 0.01);
        resized.Layout.Position.X.Should().Be(8);
        resized.Layout.Position.Y.Should().Be(14);
        resized.Layout.Anchor.BlockId.Should().Be("anchor-1");
        resized.Layout.Wrap.Mode.Should().Be(DocumentWrapMode.Square);
        resized.Layout.Stacking.ZIndex.Should().Be(4);
        command.StartTransform.Width.Should().Be(220);
        command.EndTransform.Width.Should().Be(360);
        command.InvalidatesLayout.Should().BeTrue();
        command.InvalidatedBlockIds.Should().ContainSingle().Which.Should().Be("img-1");
        stack.NextUndoDescription.Should().Be("Resize evidence image");

        await stack.UndoAsync();

        var restored = ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content);
        restored.Layout.Transform.Width.Should().Be(220);
        restored.Layout.Transform.Height.Should().Be(124);
        restored.Size.Width.Should().Be(220);
        restored.Size.Height.Should().Be(124);
        restored.Layout.Position.X.Should().Be(12);

        await stack.RedoAsync();

        var redone = ((ImageBlockContent)document.Blocks.Single(block => block.Id == "img-1").Content);
        redone.Layout.Transform.Width.Should().Be(360);
        redone.Layout.Transform.Height.Should().BeApproximately(202.91, 0.01);
    }

    [Fact]
    public async Task ResizeImageObjectCommand_SideResizeCanChangeOneAxis()
    {
        var image = ImageBlock("img-1", width: 220, height: 124);
        var document = CreateDocument(image);
        var stack = new DocumentEditorCommandStack();

        await stack.PushAsync(new ResizeImageObjectCommand(
            document,
            "img-1",
            new DocumentObjectTransform { Width = 220, Height = 124, LockAspectRatio = false },
            new DocumentObjectTransform { Width = 300, Height = 124, LockAspectRatio = false },
            constraints: new ResizeImageObjectConstraints { PreserveAspectRatio = false }));

        var resized = ((ImageBlockContent)document.Blocks.Single().Content);
        resized.Layout.Transform.Width.Should().Be(300);
        resized.Layout.Transform.Height.Should().Be(124);
        resized.Layout.Transform.LockAspectRatio.Should().BeFalse();
        resized.Size.Width.Should().Be(300);
        resized.Size.Height.Should().Be(124);
    }

    [Fact]
    public async Task ResizeImageObjectCommand_ClampsToMinimumSize()
    {
        var image = ImageBlock("img-1", width: 220, height: 124);
        var document = CreateDocument(image);
        var stack = new DocumentEditorCommandStack();

        await stack.PushAsync(new ResizeImageObjectCommand(
            document,
            "img-1",
            new DocumentObjectTransform { Width = 220, Height = 124, LockAspectRatio = false },
            new DocumentObjectTransform { Width = 8, Height = 10, LockAspectRatio = false },
            constraints: new ResizeImageObjectConstraints
            {
                MinWidth = 48,
                MinHeight = 32,
                PreserveAspectRatio = false
            }));

        var resized = ((ImageBlockContent)document.Blocks.Single().Content);
        resized.Layout.Transform.Width.Should().Be(48);
        resized.Layout.Transform.Height.Should().Be(32);
    }

    [Fact]
    public async Task ResizeImageObjectCommand_ClampsToPageBodyMaximum()
    {
        var image = ImageBlock("img-1", width: 220, height: 124);
        var document = CreateDocument(image);
        var stack = new DocumentEditorCommandStack();

        await stack.PushAsync(new ResizeImageObjectCommand(
            document,
            "img-1",
            new DocumentObjectTransform { Width = 220, Height = 124, LockAspectRatio = false },
            new DocumentObjectTransform { Width = 1200, Height = 800, LockAspectRatio = false },
            constraints: new ResizeImageObjectConstraints
            {
                MaxWidth = 500,
                MaxHeight = 300,
                PreserveAspectRatio = false
            }));

        var resized = ((ImageBlockContent)document.Blocks.Single().Content);
        resized.Layout.Transform.Width.Should().Be(500);
        resized.Layout.Transform.Height.Should().Be(300);
        resized.Size.Width.Should().Be(500);
        resized.Size.Height.Should().Be(300);
    }

    [Fact]
    public async Task ImageZOrderCommands_UpdateStackingAndUndoRedo()
    {
        var back = ImageBlock("img-back", width: 220, height: 124);
        var middle = ImageBlock("img-middle", width: 220, height: 124);
        var front = ImageBlock("img-front", width: 220, height: 124);
        GetImage(back).Layout.Stacking.ZIndex = 1;
        GetImage(middle).Layout.Stacking.ZIndex = 5;
        GetImage(front).Layout.Stacking.ZIndex = 9;
        var document = CreateDocument(back, middle, front);
        var stack = new DocumentEditorCommandStack();

        var bringForward = new BringForwardCommand(document, "img-middle");
        await stack.PushAsync(bringForward);

        GetImage(middle).Layout.Stacking.ZIndex.Should().Be(6);
        bringForward.BeforeZIndex.Should().Be(5);
        bringForward.AfterZIndex.Should().Be(6);
        bringForward.InvalidatesLayout.Should().BeTrue();
        bringForward.InvalidatedBlockIds.Should().ContainSingle().Which.Should().Be("img-middle");

        await stack.UndoAsync();
        GetImage(middle).Layout.Stacking.ZIndex.Should().Be(5);
        await stack.RedoAsync();
        GetImage(middle).Layout.Stacking.ZIndex.Should().Be(6);

        await stack.PushAsync(new SendBackwardCommand(document, "img-middle"));
        GetImage(middle).Layout.Stacking.ZIndex.Should().Be(5);
        await stack.UndoAsync();
        GetImage(middle).Layout.Stacking.ZIndex.Should().Be(6);

        await stack.PushAsync(new BringToFrontCommand(document, "img-back"));
        GetImage(back).Layout.Stacking.ZIndex.Should().Be(10);
        stack.NextUndoDescription.Should().Be("Bring image to front");
        await stack.UndoAsync();
        GetImage(back).Layout.Stacking.ZIndex.Should().Be(1);

        await stack.PushAsync(new SendToBackCommand(document, "img-front"));
        GetImage(front).Layout.Stacking.ZIndex.Should().Be(0);
        await stack.UndoAsync();
        GetImage(front).Layout.Stacking.ZIndex.Should().Be(9);
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
    public async Task SnapshotCommand_TakesOwnershipOfSnapshots_WithoutDefensiveCloning()
    {
        // Perf plan N3.1: every call-site already hands the command dedicated clones, so the
        // constructor must NOT deep-clone again. Ownership contract: a post-construction mutation
        // of the passed snapshot is visible to the command.
        var target = CreateDocument(Paragraph("Before"));
        var before = Clone(target);
        var after = Clone(target);
        var command = new DocumentEditorSnapshotCommand(target, before, after, "Ownership");

        after.Theme.BodyFontFamily = "Mutated After Construction";
        before.Theme.BodyFontFamily = "Mutated Before Construction";

        await command.ExecuteAsync();
        target.Theme.BodyFontFamily.Should().Be("Mutated After Construction");

        await command.UndoAsync();
        target.Theme.BodyFontFamily.Should().Be("Mutated Before Construction");
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

    private static DocumentBlock ImageBlock(string id, double width, double height) => new()
    {
        Id = id,
        Type = DocumentBlockType.Image,
        Content = new ImageBlockContent
        {
            Source = DocumentImageSource.Asset,
            AssetId = "asset-1",
            AltText = "Evidence",
            Size = new DocumentImageSize
            {
                Width = width,
                Height = height,
                LockAspectRatio = true
            },
            Layout = new DocumentObjectLayout
            {
                Kind = DocumentObjectLayoutKind.Anchored,
                Anchor = new DocumentObjectAnchor
                {
                    BlockId = "anchor-1",
                    MoveWithText = true,
                    FixedOnPage = false,
                    LockAnchor = true
                },
                Position = new DocumentObjectPosition
                {
                    HorizontalRelativeTo = DocumentRelativePosition.Page,
                    VerticalRelativeTo = DocumentRelativePosition.Page,
                    X = 12,
                    Y = 18,
                    HorizontalAlignment = DocumentImageHorizontalPosition.Left
                },
                Wrap = new DocumentObjectWrap
                {
                    Mode = DocumentWrapMode.Square,
                    DistanceRight = 12,
                    DistanceBottom = 8
                },
                Transform = new DocumentObjectTransform
                {
                    Width = width,
                    Height = height,
                    LockAspectRatio = true
                },
                Stacking = new DocumentObjectStacking
                {
                    ZIndex = 4,
                    AllowOverlap = true
                }
            }
        }
    };

    private static ImageBlockContent GetImage(DocumentBlock block)
        => block.Content.Should().BeOfType<ImageBlockContent>().Subject;

    private static string TextOf(DocumentBlock block)
    {
        return block.Content is ParagraphBlockContent paragraph
            ? string.Concat(paragraph.Inlines.OfType<TextRun>().Select(run => run.Text))
            : string.Empty;
    }

    private static DocumentEditorDocument Clone(DocumentEditorDocument document)
        => DocumentEditorJson.Deserialize(DocumentEditorJson.Serialize(document));
}
