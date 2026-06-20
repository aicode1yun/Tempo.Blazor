using FluentAssertions;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentWysiwygOperationMapperTests
{
    private readonly DocumentWysiwygOperationMapper _mapper = new();

    [Fact]
    public void CreateBatch_MapsInsertTextPatchToGranularOperation()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "!",
            TransactionId = "tx-1",
            Selection = Selection(offset: 5)
        };

        var batch = _mapper.CreateBatch(document, patch, Metadata());

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.InsertText);
        operation.Text.Should().Be("!");
        operation.Target.BlockId.Should().Be("b1");
        operation.Target.InlineId.Should().Be("i1");
        operation.Target.InlineIndex.Should().Be(0);
        operation.Target.Offset.Should().Be(5);
        operation.Target.Length.Should().Be(1);
        operation.Metadata.TransactionId.Should().Be("tx-1");
    }

    [Fact]
    public void CreateBatch_MapsDeleteRangePatchToGranularOperation()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "DeleteRange",
            DeleteLength = 2,
            Selection = Selection(offset: 1)
        };

        var batch = _mapper.CreateBatch(document, patch, Metadata());

        var operation = batch.Operations.Should().ContainSingle().Subject;
        operation.Type.Should().Be(DocumentOperationType.DeleteText);
        operation.Text.Should().Be("lp");
        operation.Target.BlockId.Should().Be("b1");
        operation.Target.InlineId.Should().Be("i1");
        operation.Target.Offset.Should().Be(1);
        operation.Target.Length.Should().Be(2);
    }

    [Fact]
    public void CreateBatch_MapsBackwardDeletePatchToDeletedRangeStart()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentBackward",
            Data = "ha",
            Selection = Selection(offset: 4)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.DeleteText);
        operation.Target.Offset.Should().Be(2);
        operation.Target.Length.Should().Be(2);
        operation.Text.Should().Be("ha");
    }

    [Fact]
    public void CreateBatch_MapsTrackedDeletionWithoutPlainTextSnapshotFallback()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentForward",
            Data = "Al",
            RevisionId = "rev-1",
            RevisionType = "Deletion",
            Selection = Selection(offset: 0)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.CreateRevision);
        operation.AttributeName.Should().BeNull();
        operation.AttributeValueJson.Should().BeNull();
        operation.Revision!.Type.Should().Be(DocumentRevisionType.Deletion);
        operation.Metadata.RevisionId.Should().Be("rev-1");
        operation.Metadata.RevisionType.Should().Be("Deletion");
    }

    [Fact]
    public void CreateBatch_MapsTrackedInsertionToCreateRevision()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "Draft ",
            RevisionId = "rev-insert",
            RevisionType = "Insertion",
            Selection = Selection(offset: 2)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.CreateRevision);
        operation.Text.Should().Be("Draft ");
        operation.Revision.Should().NotBeNull();
        operation.Revision!.Id.Should().Be("rev-insert");
        operation.Revision.Type.Should().Be(DocumentRevisionType.Insertion);
        operation.Revision.Action.Should().Be(DocumentRevisionAction.Pending);
        operation.Revision.PayloadJson.Should().Be("Draft ");
        operation.Revision.Range.BlockId.Should().Be("b1");
        operation.Revision.Range.StartOffset.Should().Be(2);
        operation.Revision.Range.EndOffset.Should().Be(8);
    }

    [Fact]
    public void CreateBatch_MapsTrackedDeletionToCreateRevisionWithOriginalTextAndRange()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "DeleteContentForward",
            DeleteLength = 2,
            RevisionId = "rev-delete",
            RevisionType = "Deletion",
            Selection = Selection(offset: 1)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.CreateRevision);
        operation.Text.Should().Be("lp");
        operation.Revision.Should().NotBeNull();
        operation.Revision!.Id.Should().Be("rev-delete");
        operation.Revision.Type.Should().Be(DocumentRevisionType.Deletion);
        operation.Revision.PayloadJson.Should().Be("lp");
        operation.Target.BlockId.Should().Be("b1");
        operation.Target.Offset.Should().Be(1);
        operation.Target.Length.Should().Be(2);
    }

    [Theory]
    [InlineData(DocumentRevisionAction.Accepted, DocumentOperationType.AcceptRevision)]
    [InlineData(DocumentRevisionAction.Rejected, DocumentOperationType.RejectRevision)]
    public void CreateReviewRevision_MapsReviewOperation(DocumentRevisionAction action, DocumentOperationType expectedType)
    {
        var revision = new DocumentRevision
        {
            Id = "rev-review",
            Type = DocumentRevisionType.Insertion,
            Action = DocumentRevisionAction.Pending,
            Range = new DocumentRevisionRange
            {
                BlockId = "b1",
                StartInlineIndex = 0,
                StartOffset = 1,
                EndOffset = 4
            },
            PayloadJson = "abc"
        };

        var operation = action == DocumentRevisionAction.Accepted
            ? _mapper.CreateAcceptRevision(revision, Metadata())
            : _mapper.CreateRejectRevision(revision, Metadata());

        operation.Type.Should().Be(expectedType);
        operation.Revision!.Id.Should().Be("rev-review");
        operation.Metadata.RevisionId.Should().Be("rev-review");
        operation.Target.BlockId.Should().Be("b1");
        operation.Target.Offset.Should().Be(1);
        operation.Target.Length.Should().Be(3);
    }

    [Fact]
    public void CreateBatch_KeepsMergedTypingAsSingleTextOperation()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "InsertText",
            Data = "abc",
            TransactionId = "typing-1",
            Selection = Selection(offset: 5)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.InsertText);
        operation.Text.Should().Be("abc");
        operation.Target.Length.Should().Be(3);
        operation.Metadata.TransactionId.Should().Be("typing-1");
    }

    [Fact]
    public void CreateBatch_MapsToggleBoldToAddInlineMark()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = Selection(anchorOffset: 1, focusOffset: 4)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        operation.Target.BlockId.Should().Be("b1");
        operation.Target.InlineId.Should().Be("i1");
        operation.Target.Offset.Should().Be(1);
        operation.Target.Length.Should().Be(3);
        operation.Mark.Should().NotBeNull();
        operation.Mark!.Type.Should().Be(InlineMarkType.Bold);
    }

    [Fact]
    public void CreateBatch_MapsExistingBoldSelectionToRemoveInlineMark()
    {
        var document = CreateDocument();
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = Selection(anchorOffset: 0, focusOffset: 5)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.RemoveInlineMark);
        operation.Mark!.Type.Should().Be(InlineMarkType.Bold);
    }

    [Theory]
    [InlineData("Italic", InlineMarkType.Italic)]
    [InlineData("Underline", InlineMarkType.Underline)]
    public void CreateBatch_MapsSupportedTextMarks(string markType, InlineMarkType expected)
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = markType,
            Selection = Selection(anchorOffset: 0, focusOffset: 2)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        operation.Mark!.Type.Should().Be(expected);
        operation.AttributeValueJson.Should().BeNull();
    }

    [Fact]
    public void CreateBatch_MapsLinkToInlineMarkPayload()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Link",
            Data = " https://example.test ",
            LinkTitle = "Reference",
            Selection = Selection(anchorOffset: 0, focusOffset: 5)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        operation.Mark!.Type.Should().Be(InlineMarkType.Link);
        operation.Mark.Link.Should().NotBeNull();
        operation.Mark.Link!.Href.Should().Be("https://example.test");
        operation.Mark.Link.Title.Should().Be("Reference");
        operation.AttributeName.Should().BeNull();
        operation.AttributeValueJson.Should().BeNull();
    }

    [Fact]
    public void CreateBatch_PreservesJsRuntimeOperationId()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            OperationId = "runtime-op-1",
            MarkType = "Bold",
            Selection = Selection(anchorOffset: 0, focusOffset: 5)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.OperationId.Should().Be("runtime-op-1");
    }

    [Fact]
    public void CreateBatch_MapsParagraphFormattingToBlockAttributeOperation()
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "SetParagraphProperties",
            OperationId = "paragraph-op-1",
            TransactionId = "cmd-1",
            Selection = Selection(offset: 0),
            ParagraphProperties = new DocumentParagraphPropertiesPatch
            {
                Alignment = DocumentTextAlignment.Right,
                LineSpacing = 1.5
            }
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.OperationId.Should().Be("paragraph-op-1");
        operation.Type.Should().Be(DocumentOperationType.SetBlockAttribute);
        operation.Target.BlockId.Should().Be("b1");
        operation.AttributeName.Should().Be("paragraphProperties");
        var payload = JsonSerializer.Deserialize<DocumentParagraphPropertiesPatch>(operation.AttributeValueJson!, DocumentEditorJson.Options);
        payload!.Alignment.Should().Be(DocumentTextAlignment.Right);
        payload.LineSpacing.Should().Be(1.5);
        operation.Metadata.TransactionId.Should().Be("cmd-1");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,boom")]
    public void CreateBatch_RejectsUnsafeLinkTargets(string href)
    {
        var document = CreateDocument();
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Link",
            Data = href,
            Selection = Selection(anchorOffset: 0, focusOffset: 5)
        };

        _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().BeEmpty();
    }

    [Fact]
    public void CreateBatch_MapsCommentAndRevisionMarkPayloads()
    {
        var document = CreateDocument();
        var commentPatch = new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = "CommentAnchor",
            Data = "comment-1",
            Selection = Selection(anchorOffset: 0, focusOffset: 2)
        };
        var revisionPatch = new WysiwygPatch
        {
            Type = "SetMarks",
            MarkType = "Revision",
            RevisionId = "rev-1",
            RevisionType = "FormatChange",
            Selection = Selection(anchorOffset: 2, focusOffset: 5)
        };

        var commentOperation = _mapper.CreateBatch(document, commentPatch, Metadata()).Operations.Should().ContainSingle().Subject;
        var revisionOperation = _mapper.CreateBatch(document, revisionPatch, Metadata()).Operations.Should().ContainSingle().Subject;

        commentOperation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        commentOperation.Mark!.Type.Should().Be(InlineMarkType.CommentAnchor);
        commentOperation.Mark.CommentAnchor!.CommentId.Should().Be("comment-1");
        commentOperation.Mark.CommentAnchor.AnchorId.Should().Be("comment-1");
        revisionOperation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        revisionOperation.Mark!.Type.Should().Be(InlineMarkType.Revision);
        revisionOperation.Mark.RevisionId.Should().Be("rev-1");
        revisionOperation.Mark.Value.Should().Be("FormatChange");
    }

    [Fact]
    public void CreateBatch_MapsMarkSelectionAcrossMultipleInlinesToBlockRange()
    {
        var document = CreateDocument();
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines.Add(new TextRun { Id = "i2", Text = "Beta" });
        var patch = new WysiwygPatch
        {
            Type = "ToggleMark",
            MarkType = "Bold",
            Selection = new WysiwygSelectionSnapshot
            {
                AnchorBlockId = "b1",
                AnchorInlineId = "i1",
                AnchorOffset = 1,
                FocusBlockId = "b1",
                FocusInlineId = "i2",
                FocusOffset = 2,
                IsCollapsed = false
            }
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.AddInlineMark);
        operation.Target.BlockId.Should().Be("b1");
        operation.Target.InlineId.Should().BeNull();
        operation.Target.Offset.Should().Be(1);
        operation.Target.Length.Should().Be(6);
        operation.Mark!.Type.Should().Be(InlineMarkType.Bold);
    }

    [Fact]
    public void CreateBatch_MapsParagraphInsertBlockWithFullPayload()
    {
        var document = CreateDocument();
        document.Blocks[0].Order = 10;
        var block = ParagraphBlock("b2", "Inserted", order: 0);
        var patch = new WysiwygPatch
        {
            Type = "InsertBlock",
            Block = block,
            Selection = Selection(offset: 5)
        };

        var operation = _mapper.CreateBatch(document, patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.InsertBlock);
        operation.Target.BlockId.Should().Be("b2");
        operation.Target.Order.Should().Be(11);
        operation.Block!.Id.Should().Be(block.Id);
        operation.Block.Type.Should().Be(block.Type);
        Serialize(operation.Block.Content).Should().Be(Serialize(block.Content));
        operation.Block!.Order.Should().Be(11);
    }

    [Fact]
    public void CreateBatch_MapsHeadingLevelUpdateToBlockAttribute()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "h1",
                Type = DocumentBlockType.Heading,
                Content = new HeadingBlockContent
                {
                    Level = 1,
                    Inlines = [new TextRun { Id = "hi1", Text = "Title" }]
                }
            }
        ];
        var updated = Clone(document.Blocks[0]);
        ((HeadingBlockContent)updated.Content).Level = 3;

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "UpdateBlock", Block = updated }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.SetBlockAttribute);
        operation.AttributeName.Should().Be("headingLevel");
        JsonSerializer.Deserialize<int>(operation.AttributeValueJson!, DocumentEditorJson.Options).Should().Be(3);
        operation.Block.Should().BeNull();
    }

    [Fact]
    public void CreateBatch_MapsRemoveBlockToIdempotentDeleteBlock()
    {
        var patch = new WysiwygPatch
        {
            Type = "RemoveBlock",
            Selection = Selection(offset: 0)
        };

        var operation = _mapper.CreateBatch(CreateDocument(), patch, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.DeleteBlock);
        operation.Target.BlockId.Should().Be("b1");
    }

    [Fact]
    public void CreateBatch_MapsImageInsertBlockWithProviderAssetPayload()
    {
        var document = CreateDocument();
        var image = ImageBlock("img-1", "asset-42", "Before signature", width: 320);

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "InsertBlock", Block = image }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.InsertBlock);
        operation.Block!.Id.Should().Be(image.Id);
        operation.Block.Type.Should().Be(image.Type);
        Serialize(operation.Block.Content).Should().Be(Serialize(image.Content));
        var content = operation.Block!.Content.Should().BeOfType<ImageBlockContent>().Subject;
        content.Source.Should().Be(DocumentImageSource.Asset);
        content.AssetId.Should().Be("asset-42");
        content.Url.Should().Be("/api/document-assets/asset-42");
        content.AltText.Should().Be("Before signature");
        content.Size.Width.Should().Be(320);
    }

    [Fact]
    public void CreateBatch_MapsImageAltTextUpdateToUpdateBlock()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks = [ImageBlock("img-1", "asset-42", "Old", width: 320)];
        var updated = Clone(document.Blocks[0]);
        ((ImageBlockContent)updated.Content).AltText = "New";

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "UpdateBlock", Block = updated }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.UpdateBlock);
        operation.AttributeName.Should().BeNull();
        ((ImageBlockContent)operation.Block!.Content).AltText.Should().Be("New");
    }

    [Fact]
    public void CreateBatch_MapsImageLayoutUpdateToStructuredUpdateBlock()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks = [ImageBlock("img-1", "asset-42", "Image", width: 320)];
        var updated = Clone(document.Blocks[0]);
        var image = (ImageBlockContent)updated.Content;
        image.Size.Width = 480;
        image.FloatingLayout = new DocumentFloatingLayout
        {
            Inline = false,
            WrapMode = DocumentWrapMode.Square,
            X = 24,
            Y = 48
        };

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "UpdateBlock", Block = updated }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.UpdateBlock);
        operation.Text.Should().BeNull();
        operation.AttributeName.Should().BeNull();
        ((ImageBlockContent)operation.Block!.Content).FloatingLayout.Should().NotBeNull();
    }

    [Fact]
    public void CreateBatch_MapsImageMoveBlockToMoveBlockOperation()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks =
        [
            ParagraphBlock("p1", "Before", 10),
            ImageBlock("img-1", "asset-42", "Image", width: 320),
            ParagraphBlock("p2", "After", 30)
        ];

        var operation = _mapper.CreateBatch(document, new WysiwygPatch
        {
            Type = "MoveBlock",
            Block = new DocumentBlock
            {
                Id = "img-1",
                Type = DocumentBlockType.Image,
                Order = 35,
                Content = ((DocumentBlock)document.Blocks[1]).Content
            },
            Selection = new WysiwygSelectionSnapshot { AnchorBlockId = "img-1" }
        }, Metadata()).Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.MoveBlock);
        operation.Target.BlockId.Should().Be("img-1");
        operation.Target.Order.Should().Be(35);
        operation.Block.Should().BeNull();
    }

    [Fact]
    public void CreateBatch_MapsTableInsertBlockAsStructuralPayload()
    {
        var document = CreateDocument();
        var table = TableBlock("table-1", ("cell-1", "Alpha"), ("cell-2", "Beta"));

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "InsertBlock", Block = table }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.InsertBlock);
        operation.Block!.Content.Should().BeOfType<TableBlockContent>();
        operation.Text.Should().BeNull();
    }

    [Fact]
    public void CreateBatch_MapsSingleTableCellTextEditToCellOperation()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks = [TableBlock("table-1", ("cell-1", "Alpha"), ("cell-2", "Beta"))];
        var updated = Clone(document.Blocks[0]);
        var cell = ((TableBlockContent)updated.Content).Rows[0].Cells[1];
        ((TextRun)((ParagraphBlockContent)cell.Blocks[0].Content).Inlines[0]).Text = "Beta edited";

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "UpdateBlock", Block = updated }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.SetBlockAttribute);
        operation.AttributeName.Should().Be("table.cell.text");
        operation.Target.BlockId.Should().Be("table-1");
        operation.Target.TableCellId.Should().Be("cell-2");
        JsonSerializer.Deserialize<string>(operation.AttributeValueJson!, DocumentEditorJson.Options).Should().Be("Beta edited");
    }

    [Fact]
    public void CreateBatch_MapsTableRowOrColumnChangeToStructuralUpdateBlock()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks = [TableBlock("table-1", ("cell-1", "Alpha"), ("cell-2", "Beta"))];
        var updated = Clone(document.Blocks[0]);
        ((TableBlockContent)updated.Content).Rows.Add(new TableRowContent
        {
            Cells = [TableCell("cell-3", "Gamma"), TableCell("cell-4", "Delta")]
        });

        var operation = _mapper.CreateBatch(document, new WysiwygPatch { Type = "UpdateBlock", Block = updated }, Metadata())
            .Operations.Should().ContainSingle().Subject;

        operation.Type.Should().Be(DocumentOperationType.UpdateBlock);
        operation.Block!.Content.Should().BeOfType<TableBlockContent>();
        operation.Target.TableCellId.Should().BeNull();
    }

    private static DocumentEditorDocument CreateDocument()
    {
        var document = DocumentOperationEngineTests.CreateDocument("doc-1", "b1", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "i1";
        return document;
    }

    private static WysiwygSelectionSnapshot Selection(int offset)
        => new()
        {
            AnchorBlockId = "b1",
            AnchorInlineId = "i1",
            AnchorOffset = offset,
            FocusBlockId = "b1",
            FocusInlineId = "i1",
            FocusOffset = offset,
            IsCollapsed = true
        };

    private static WysiwygSelectionSnapshot Selection(int anchorOffset, int focusOffset)
        => new()
        {
            AnchorBlockId = "b1",
            AnchorInlineId = "i1",
            AnchorOffset = anchorOffset,
            FocusBlockId = "b1",
            FocusInlineId = "i1",
            FocusOffset = focusOffset,
            IsCollapsed = anchorOffset == focusOffset
        };

    private static DocumentOperationMetadata Metadata()
        => new()
        {
            AuthorId = "author-1",
            ClientId = "client-1",
            LogicalTimestamp = 10
        };

    private static DocumentBlock ParagraphBlock(string id, string text, double order = 0)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Order = order,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = $"{id}-inline", Text = text }]
            }
        };

    private static DocumentBlock ImageBlock(string id, string assetId, string altText, double width)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Image,
            Order = 20,
            Content = new ImageBlockContent
            {
                Source = DocumentImageSource.Asset,
                AssetId = assetId,
                Url = $"/api/document-assets/{assetId}",
                AltText = altText,
                Alignment = DocumentImageAlignment.Center,
                Size = new DocumentImageSize { Width = width, Height = 180 }
            }
        };

    private static DocumentBlock TableBlock(string id, params (string CellId, string Text)[] cells)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Table,
            Order = 20,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells = cells.Select(cell => TableCell(cell.CellId, cell.Text)).ToList()
                    }
                ]
            }
        };

    private static TableCellContent TableCell(string id, string text)
        => new()
        {
            Id = id,
            Blocks =
            [
                new DocumentBlock
                {
                    Id = $"{id}-block",
                    Type = DocumentBlockType.Paragraph,
                    Content = new ParagraphBlockContent
                    {
                        Inlines = [new TextRun { Id = $"{id}-inline", Text = text }]
                    }
                }
            ]
        };

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, DocumentEditorJson.Options);
}
