using FluentAssertions;
using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.DocumentEditor;

public sealed class DocumentOperationEngineTests
{
    [Fact]
    public void OperationLog_AppendsBatchesInOrder()
    {
        var log = new DocumentOperationLog();

        log.Append(Batch("doc-1", InsertBlock("a", 0, "Alpha"))).IsValid.Should().BeTrue();
        log.Append(Batch("doc-1", InsertBlock("b", 1, "Beta"))).IsValid.Should().BeTrue();

        log.Batches.SelectMany(batch => batch.Operations).Select(operation => operation.Target.BlockId)
            .Should().Equal("a", "b");
    }

    [Fact]
    public void OperationApplier_ReplaysOperationsOnEmptyDocument()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        var log = new DocumentOperationLog();
        log.Append(Batch("doc-1", InsertBlock("a", 0, "Alpha")));
        log.Append(Batch("doc-1", InsertText("a", 5, " text")));

        var result = log.Replay(document);

        result.IsValid.Should().BeTrue();
        TextOf(document, "a").Should().Be("Alpha text");
    }

    [Fact]
    public void OperationApplier_ReplaysOperationsOnExistingDocument()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var applier = new DocumentOperationApplier();

        var result = applier.Apply(document, Batch("doc-1", DeleteText("a", 2, "ph"), InsertText("a", 2, "ZZ")));

        result.IsValid.Should().BeTrue();
        TextOf(document, "a").Should().Be("AlZZa");
    }

    [Fact]
    public void OperationApplier_AppliesInsertTextByInlineId()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";
        var operation = InsertText("a", 5, "!");
        operation.Target.InlineId = "inline-a";
        operation.Target.InlineIndex = 99;

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        TextOf(document, "a").Should().Be("Alpha!");
    }

    [Fact]
    public void OperationApplier_AppliesDeleteTextByExplicitLength()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var operation = DeleteText("a", 1, string.Empty);
        operation.Target.Length = 2;

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        TextOf(document, "a").Should().Be("Aha");
    }

    [Fact]
    public void OperationApplier_AppliesAddInlineMarkToRange()
    {
        var document = CreateDocument("doc-1", "a", "Hello world");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";
        var operation = AddInlineMark("a", "inline-a", offset: 6, length: 5, InlineMarkType.Bold);

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("Hello ", "world");
        inlines[0].Marks.Should().BeEmpty();
        inlines[1].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void OperationApplier_AppliesRemoveInlineMarkWithoutLosingText()
    {
        var document = CreateDocument("doc-1", "a", "Hello world");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0] = new TextRun
        {
            Id = "inline-a",
            Text = "Hello world",
            Marks = [new InlineMark { Type = InlineMarkType.Bold }]
        };
        var operation = AddInlineMark("a", "inline-a", offset: 6, length: 5, InlineMarkType.Bold);
        operation.Type = DocumentOperationType.RemoveInlineMark;

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("Hello ", "world");
        inlines[0].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
        inlines[1].Marks.Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_AppliesInlineMarkRangeToTargetInlineOnly()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines[0].Id = "inline-a";
        paragraph.Inlines.Add(new TextRun { Id = "inline-b", Text = "Beta" });
        var operation = AddInlineMark("a", "inline-b", offset: 1, length: 2, InlineMarkType.Italic);

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("AlphaB", "et", "a");
        inlines[0].Marks.Should().BeEmpty();
        inlines[1].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Italic);
        inlines[2].Marks.Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_MapsBlankInlineIdMarkOffsetAcrossSplitRuns()
    {
        var document = CreateDocument("doc-1", "a", "Hello world");
        var applier = new DocumentOperationApplier();
        var bold = AddInlineMark("a", "", offset: 0, length: 5, InlineMarkType.Bold);
        var italic = AddInlineMark("a", "", offset: 6, length: 5, InlineMarkType.Italic);

        var boldResult = applier.Apply(document, Batch("doc-1", bold));
        var italicResult = applier.Apply(document, Batch("doc-1", italic));

        boldResult.IsValid.Should().BeTrue();
        italicResult.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("Hello", " ", "world");
        inlines[0].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
        inlines[1].Marks.Should().BeEmpty();
        inlines[2].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Italic);
    }

    [Fact]
    public void OperationApplier_AppliesInlineMarkAcrossMultipleRunsAndMergesCompatibleSegments()
    {
        var document = CreateDocument("doc-1", "a", string.Empty);
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines =
        [
            new TextRun { Id = "r1", Text = "Hel" },
            new TextRun { Id = "r2", Text = "lo " },
            new TextRun { Id = "r3", Text = "world" }
        ];
        var operation = AddInlineMark("a", "", offset: 2, length: 7, InlineMarkType.Bold);

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("He", "llo wor", "ld");
        inlines[0].Marks.Should().BeEmpty();
        inlines[1].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.Bold);
        inlines[2].Marks.Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_RemovesInlineMarkAcrossMultipleRunsAndMergesBack()
    {
        var document = CreateDocument("doc-1", "a", string.Empty);
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines =
        [
            new TextRun { Text = "He" },
            new TextRun { Text = "llo ", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
            new TextRun { Text = "wor", Marks = [new InlineMark { Type = InlineMarkType.Bold }] },
            new TextRun { Text = "ld" }
        ];
        var operation = AddInlineMark("a", "", offset: 2, length: 7, InlineMarkType.Bold);
        operation.Type = DocumentOperationType.RemoveInlineMark;

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var run = InlinesOf(document, "a").OfType<TextRun>().Should().ContainSingle().Subject;
        run.Text.Should().Be("Hello world");
        run.Marks.Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_DoesNotMergeRunsWithDifferentCommentOrRevisionMembership()
    {
        var document = CreateDocument("doc-1", "a", string.Empty);
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines =
        [
            new TextRun
            {
                Text = "Alpha",
                Marks =
                [
                    new InlineMark
                    {
                        Type = InlineMarkType.CommentAnchor,
                        CommentAnchor = new CommentAnchorMarkData { CommentId = "comment-1", AnchorId = "comment-1" }
                    }
                ]
            },
            new TextRun
            {
                Text = "Beta",
                Marks =
                [
                    new InlineMark
                    {
                        Type = InlineMarkType.Revision,
                        RevisionId = "revision-1",
                        Value = DocumentRevisionType.Formatting.ToString()
                    }
                ]
            }
        ];
        var operation = AddInlineMark("a", "", offset: 0, length: 9, InlineMarkType.Highlight);

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("Alpha", "Beta");
        inlines[0].Marks.Should().Contain(mark => mark.Type == InlineMarkType.CommentAnchor);
        inlines[0].Marks.Should().Contain(mark => mark.Type == InlineMarkType.Highlight);
        inlines[1].Marks.Should().Contain(mark => mark.Type == InlineMarkType.Revision);
        inlines[1].Marks.Should().Contain(mark => mark.Type == InlineMarkType.Highlight);
    }

    [Fact]
    public void OperationApplier_ValueMarkReplacesExistingValueWithoutChangingSurroundingText()
    {
        var document = CreateDocument("doc-1", "a", string.Empty);
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines =
        [
            new TextRun
            {
                Text = "Hello world",
                Marks = [new InlineMark { Type = InlineMarkType.TextColor, Value = "#111111" }]
            }
        ];
        var operation = AddInlineMark("a", "", offset: 6, length: 5, InlineMarkType.TextColor);
        operation.Mark!.Value = "#2563eb";

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        var inlines = InlinesOf(document, "a").OfType<TextRun>().ToList();
        inlines.Select(run => run.Text).Should().Equal("Hello ", "world");
        inlines[0].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.TextColor && mark.Value == "#111111");
        inlines[1].Marks.Should().ContainSingle(mark => mark.Type == InlineMarkType.TextColor && mark.Value == "#2563eb");
    }

    [Fact]
    public void Operations_ExposeOperationIdAndLegacyIdAlias()
    {
        var operation = InsertText("a", 5, "!");

        operation.OperationId.Should().NotBeNullOrWhiteSpace();
        operation.Id.Should().Be(operation.OperationId);

        operation.Id = "legacy-id";

        operation.OperationId.Should().Be("legacy-id");
    }

    [Fact]
    public void Operations_RoundtripOperationIdAndReadLegacyId()
    {
        var operation = InsertText("a", 5, "!");
        operation.OperationId = "operation-1";

        var json = JsonSerializer.Serialize(operation, DocumentEditorJson.Options);
        var roundtrip = JsonSerializer.Deserialize<DocumentOperation>(json, DocumentEditorJson.Options)!;
        var legacy = JsonSerializer.Deserialize<DocumentOperation>(
            """
            {
                "Id": "legacy-operation",
                "SchemaVersion": 1,
                "Type": 0,
                "Target": { "BlockId": "a", "InlineIndex": 0, "Offset": 0 },
                "Text": "x"
            }
            """,
            DocumentEditorJson.Options)!;

        json.Should().Contain(nameof(DocumentOperation.OperationId));
        json.Should().NotContain("\"Id\"");
        roundtrip.OperationId.Should().Be("operation-1");
        legacy.OperationId.Should().Be("legacy-operation");
        legacy.Id.Should().Be("legacy-operation");
    }

    [Fact]
    public void OperationLog_ReplayIsIdempotentByOperationId()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var operation = InsertText("a", 5, "!");
        var log = new DocumentOperationLog();

        log.Append(Batch("doc-1", operation));
        log.Append(Batch("doc-1", operation));
        log.Replay(document);

        TextOf(document, "a").Should().Be("Alpha!");
    }

    [Fact]
    public void OperationLog_AppendingDuplicateOperationMutatesBatchToUniqueOperations()
    {
        var operation = InsertText("a", 5, "!");
        var duplicateBatch = Batch("doc-1", operation);
        var log = new DocumentOperationLog();

        log.Append(Batch("doc-1", operation)).IsValid.Should().BeTrue();
        log.Append(duplicateBatch).IsValid.Should().BeTrue();

        duplicateBatch.Operations.Should().BeEmpty();
        log.Batches.Should().ContainSingle();
    }

    [Fact]
    public void OperationLog_RejectsUnknownSchemaVersion()
    {
        var operation = InsertText("a", 0, "x");
        operation.SchemaVersion = 999;

        var result = new DocumentOperationLog().Append(Batch("doc-1", operation));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Unsupported operation schema version", StringComparison.Ordinal));
    }

    [Fact]
    public void OperationApplier_DeleteBlockIsIdempotentForDuplicateReplay()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        var operation = new DocumentOperation
        {
            Type = DocumentOperationType.DeleteBlock,
            Target = new DocumentOperationTarget { BlockId = "a" },
            Metadata = Metadata(1, "client-a")
        };
        var applier = new DocumentOperationApplier();

        applier.Apply(document, Batch("doc-1", operation)).IsValid.Should().BeTrue();
        var result = applier.Apply(document, Batch("doc-1", operation));

        result.IsValid.Should().BeTrue();
        document.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_UpdateBlockReplacesImageContent()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = "img-1",
                Type = DocumentBlockType.Image,
                Order = 10,
                Content = new ImageBlockContent
                {
                    Source = DocumentImageSource.Asset,
                    AssetId = "asset-1",
                    Url = "/assets/1.png",
                    AltText = "Old",
                    Size = new DocumentImageSize { Width = 100, Height = 50 }
                }
            }
        ];
        var updated = Clone(document.Blocks[0]);
        var image = (ImageBlockContent)updated.Content;
        image.AltText = "New";
        image.Size.Width = 240;

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", new DocumentOperation
        {
            Type = DocumentOperationType.UpdateBlock,
            Target = new DocumentOperationTarget { BlockId = "img-1" },
            Block = updated
        }));

        result.IsValid.Should().BeTrue();
        var content = document.Blocks.Single().Content.Should().BeOfType<ImageBlockContent>().Subject;
        content.AltText.Should().Be("New");
        content.Size.Width.Should().Be(240);
        document.Blocks.Single().Order.Should().Be(10);
    }

    [Fact]
    public void OperationApplier_SetHeadingLevelChangesOnlyHeadingLevel()
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
                    Inlines = [new TextRun { Id = "inline-1", Text = "Title" }]
                }
            }
        ];

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = "h1" },
            AttributeName = "headingLevel",
            AttributeValueJson = JsonSerializer.Serialize(4, DocumentEditorJson.Options)
        }));

        result.IsValid.Should().BeTrue();
        var content = document.Blocks.Single().Content.Should().BeOfType<HeadingBlockContent>().Subject;
        content.Level.Should().Be(4);
        content.Inlines.OfType<TextRun>().Single().Text.Should().Be("Title");
    }

    [Fact]
    public void OperationApplier_SetParagraphPropertiesMergesFormattingPatch()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        document.Blocks.Single().ParagraphProperties.LeftIndent = 18;

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = "a" },
            AttributeName = "paragraphProperties",
            AttributeValueJson = JsonSerializer.Serialize(new DocumentParagraphPropertiesPatch
            {
                Alignment = DocumentTextAlignment.Right,
                LineSpacing = 1.5,
                LeftIndentDelta = 18
            }, DocumentEditorJson.Options)
        }));

        result.IsValid.Should().BeTrue();
        document.Blocks.Single().ParagraphProperties.Alignment.Should().Be(DocumentTextAlignment.Right);
        document.Blocks.Single().ParagraphProperties.LineSpacing.Should().Be(1.5);
        document.Blocks.Single().ParagraphProperties.LeftIndent.Should().Be(36);
    }

    [Fact]
    public void OperationApplier_SetTableCellTextUpdatesTargetCellOnly()
    {
        var document = DocumentEditorDocument.Empty("doc-1");
        document.Blocks = [TableBlock("table-1", ("cell-1", "Alpha"), ("cell-2", "Beta"))];

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = "table-1", TableCellId = "cell-2" },
            AttributeName = "table.cell.text",
            AttributeValueJson = JsonSerializer.Serialize("Beta edited", DocumentEditorJson.Options)
        }));

        result.IsValid.Should().BeTrue();
        CellText(document, "cell-1").Should().Be("Alpha");
        CellText(document, "cell-2").Should().Be("Beta edited");
    }

    [Fact]
    public void OperationApplier_CreateRevisionInsertionAddsPendingRevisionAndMarkedText()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", CreateRevision(
            "a",
            "inline-a",
            offset: 2,
            length: 6,
            "rev-insert",
            DocumentRevisionType.Insertion,
            "Draft ")));

        result.IsValid.Should().BeTrue();
        document.Revisions.Should().ContainSingle(revision =>
            revision.Id == "rev-insert"
            && revision.Type == DocumentRevisionType.Insertion
            && revision.Action == DocumentRevisionAction.Pending);
        TextOfAllRuns(document, "a").Should().Be("AlDraft pha");
        RevisionRunsOf(document, "a").Should().ContainSingle(run => run.Text == "Draft ");
    }

    [Fact]
    public void OperationApplier_CreateRevisionDeletionKeepsTextAsPendingDeletion()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", CreateRevision(
            "a",
            "inline-a",
            offset: 1,
            length: 2,
            "rev-delete",
            DocumentRevisionType.Deletion,
            "lp")));

        result.IsValid.Should().BeTrue();
        document.Revisions.Should().ContainSingle(revision => revision.Id == "rev-delete" && revision.Type == DocumentRevisionType.Deletion);
        TextOfAllRuns(document, "a").Should().Be("Alpha");
        RevisionRunsOf(document, "a").Should().ContainSingle(run => run.Text == "lp");
    }

    [Fact]
    public void OperationApplier_AcceptRevisionInsertionKeepsTextAndClearsMark()
    {
        var document = CreateDocumentWithPendingRevision(DocumentRevisionType.Insertion, "Draft ");

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", ReviewRevision(DocumentOperationType.AcceptRevision)));

        result.IsValid.Should().BeTrue();
        document.Revisions.Single().Action.Should().Be(DocumentRevisionAction.Accepted);
        TextOfAllRuns(document, "a").Should().Be("Draft Alpha");
        RevisionRunsOf(document, "a").Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_RejectRevisionInsertionRemovesText()
    {
        var document = CreateDocumentWithPendingRevision(DocumentRevisionType.Insertion, "Draft ");

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", ReviewRevision(DocumentOperationType.RejectRevision)));

        result.IsValid.Should().BeTrue();
        document.Revisions.Single().Action.Should().Be(DocumentRevisionAction.Rejected);
        TextOfAllRuns(document, "a").Should().Be("Alpha");
        RevisionRunsOf(document, "a").Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_AcceptRevisionDeletionRemovesDeletedText()
    {
        var document = CreateDocumentWithPendingRevision(DocumentRevisionType.Deletion, "lp", prefix: "A", suffix: "ha");

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", ReviewRevision(DocumentOperationType.AcceptRevision)));

        result.IsValid.Should().BeTrue();
        document.Revisions.Single().Action.Should().Be(DocumentRevisionAction.Accepted);
        TextOfAllRuns(document, "a").Should().Be("Aha");
        RevisionRunsOf(document, "a").Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_RejectRevisionDeletionKeepsTextAndClearsMark()
    {
        var document = CreateDocumentWithPendingRevision(DocumentRevisionType.Deletion, "lp", prefix: "A", suffix: "ha");

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", ReviewRevision(DocumentOperationType.RejectRevision)));

        result.IsValid.Should().BeTrue();
        document.Revisions.Single().Action.Should().Be(DocumentRevisionAction.Rejected);
        TextOfAllRuns(document, "a").Should().Be("Alpha");
        RevisionRunsOf(document, "a").Should().BeEmpty();
    }

    [Fact]
    public void OperationApplier_CreateRevisionFormattingAddsPendingRevisionAndMark()
    {
        var document = CreateDocument("doc-1", "a", "Alpha");
        ((ParagraphBlockContent)document.Blocks[0].Content).Inlines[0].Id = "inline-a";

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", CreateFormattingRevision(new()
        {
            MarkType = InlineMarkType.Bold,
            NewActive = true
        })));

        result.IsValid.Should().BeTrue();
        document.Revisions.Should().ContainSingle(revision => revision.Type == DocumentRevisionType.Formatting);
        var revisionRun = RevisionRunsOf(document, "a").Should().ContainSingle().Subject;
        revisionRun.Text.Should().Be("lph");
        revisionRun.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold);
    }

    [Fact]
    public void OperationApplier_RejectRevisionFormattingRestoresPreviousMarkState()
    {
        var document = CreateDocumentWithPendingFormattingRevision(new()
        {
            MarkType = InlineMarkType.Bold,
            NewActive = true
        });

        var result = new DocumentOperationApplier().Apply(document, Batch("doc-1", ReviewRevision(DocumentOperationType.RejectRevision)));

        result.IsValid.Should().BeTrue();
        document.Revisions.Single().Action.Should().Be(DocumentRevisionAction.Rejected);
        RevisionRunsOf(document, "a").Should().BeEmpty();
        InlinesOf(document, "a").OfType<TextRun>().Should().NotContain(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Bold));
    }

    private static DocumentOperationBatch Batch(string documentId, params DocumentOperation[] operations)
    {
        return new DocumentOperationBatch
        {
            DocumentId = documentId,
            Operations = operations.ToList()
        };
    }

    internal static DocumentEditorDocument CreateDocument(string documentId, string blockId, string text)
    {
        var document = DocumentEditorDocument.Empty(documentId);
        document.Blocks =
        [
            new DocumentBlock
            {
                Id = blockId,
                Type = DocumentBlockType.Paragraph,
                Content = new ParagraphBlockContent
                {
                    Inlines = [new TextRun { Text = text }]
                }
            }
        ];
        return document;
    }

    internal static DocumentOperation InsertBlock(string blockId, double order, string text)
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.InsertBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, Order = order },
            Block = CreateDocument("doc-1", blockId, text).Blocks[0],
            Metadata = Metadata(1, "client-a")
        };
    }

    internal static DocumentOperation InsertText(string blockId, int offset, string text, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.InsertText,
            Target = new DocumentOperationTarget { BlockId = blockId, InlineIndex = 0, Offset = offset, Length = text.Length },
            Text = text,
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation DeleteText(string blockId, int offset, string text, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.DeleteText,
            Target = new DocumentOperationTarget { BlockId = blockId, InlineIndex = 0, Offset = offset, Length = text.Length },
            Text = text,
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation AddInlineMark(
        string blockId,
        string inlineId,
        int offset,
        int length,
        InlineMarkType markType,
        long timestamp = 1,
        string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.AddInlineMark,
            Target = new DocumentOperationTarget
            {
                BlockId = blockId,
                InlineId = inlineId,
                InlineIndex = 0,
                Offset = offset,
                Length = length
            },
            Mark = new InlineMark { Type = markType },
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation SetText(string blockId, string text, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.SetBlockAttribute,
            Target = new DocumentOperationTarget { BlockId = blockId },
            AttributeName = "text",
            AttributeValueJson = JsonSerializer.Serialize(text, DocumentEditorJson.Options),
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperation MoveBlock(string blockId, double order, long timestamp = 1, string clientId = "client-a")
    {
        return new DocumentOperation
        {
            Type = DocumentOperationType.MoveBlock,
            Target = new DocumentOperationTarget { BlockId = blockId, Order = order },
            Metadata = Metadata(timestamp, clientId)
        };
    }

    internal static DocumentOperationMetadata Metadata(long timestamp, string clientId)
    {
        return new DocumentOperationMetadata
        {
            AuthorId = clientId,
            ClientId = clientId,
            LogicalTimestamp = timestamp
        };
    }

    internal static string TextOf(DocumentEditorDocument document, string blockId)
    {
        return document.Blocks
            .First(block => block.Id == blockId)
            .Content.Should()
            .BeOfType<ParagraphBlockContent>()
            .Subject
            .Inlines
            .OfType<TextRun>()
            .Single()
            .Text;
    }

    internal static List<InlineContent> InlinesOf(DocumentEditorDocument document, string blockId)
    {
        return document.Blocks
            .First(block => block.Id == blockId)
            .Content.Should()
            .BeOfType<ParagraphBlockContent>()
            .Subject
            .Inlines;
    }

    private static DocumentOperation CreateRevision(
        string blockId,
        string inlineId,
        int offset,
        int length,
        string revisionId,
        DocumentRevisionType type,
        string text)
        => new()
        {
            Type = DocumentOperationType.CreateRevision,
            Target = new DocumentOperationTarget
            {
                BlockId = blockId,
                InlineId = inlineId,
                InlineIndex = 0,
                Offset = offset,
                Length = length
            },
            Text = text,
            Revision = new DocumentRevision
            {
                Id = revisionId,
                Type = type,
                Action = DocumentRevisionAction.Pending,
                Range = new DocumentRevisionRange
                {
                    BlockId = blockId,
                    StartInlineIndex = 0,
                    EndInlineIndex = 0,
                    StartOffset = offset,
                    EndOffset = offset + length
                },
                PayloadJson = text
            }
        };

    private static DocumentOperation ReviewRevision(DocumentOperationType type)
        => new()
        {
            Type = type,
            Revision = new DocumentRevision
            {
                Id = "rev-1",
                Type = DocumentRevisionType.Insertion,
                Range = new DocumentRevisionRange { BlockId = "a" }
            },
            Metadata = new DocumentOperationMetadata { RevisionId = "rev-1" }
        };

    private static DocumentOperation CreateFormattingRevision(DocumentFormattingRevisionPayload payload)
        => new()
        {
            Type = DocumentOperationType.CreateRevision,
            Target = new DocumentOperationTarget
            {
                BlockId = "a",
                InlineId = "inline-a",
                InlineIndex = 0,
                Offset = 1,
                Length = 3
            },
            Mark = new InlineMark { Type = payload.MarkType },
            Revision = new DocumentRevision
            {
                Id = "rev-1",
                Type = DocumentRevisionType.Formatting,
                Action = DocumentRevisionAction.Pending,
                Range = new DocumentRevisionRange
                {
                    BlockId = "a",
                    StartInlineIndex = 0,
                    EndInlineIndex = 0,
                    StartOffset = 1,
                    EndOffset = 4
                },
                PayloadJson = JsonSerializer.Serialize(payload, DocumentEditorJson.Options)
            }
        };

    private static DocumentEditorDocument CreateDocumentWithPendingRevision(
        DocumentRevisionType type,
        string text,
        string prefix = "",
        string suffix = "Alpha")
    {
        var document = CreateDocument("doc-1", "a", string.Empty);
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines =
        [
            new TextRun { Text = prefix },
            new TextRun
            {
                Id = "rev-rev-1",
                Text = text,
                Marks =
                [
                    new InlineMark
                    {
                        Type = InlineMarkType.Revision,
                        RevisionId = "rev-1",
                        Value = type.ToString()
                    }
                ]
            },
            new TextRun { Text = suffix }
        ];
        document.Revisions =
        [
            new DocumentRevision
            {
                Id = "rev-1",
                Type = type,
                Action = DocumentRevisionAction.Pending,
                Range = new DocumentRevisionRange { BlockId = "a", StartInlineIndex = 1 },
                PayloadJson = text
            }
        ];
        return document;
    }

    private static DocumentEditorDocument CreateDocumentWithPendingFormattingRevision(DocumentFormattingRevisionPayload payload)
    {
        var document = CreateDocument("doc-1", "a", string.Empty);
        var paragraph = (ParagraphBlockContent)document.Blocks[0].Content;
        paragraph.Inlines =
        [
            new TextRun { Text = "A" },
            new TextRun
            {
                Id = "rev-rev-1",
                Text = "lph",
                Marks =
                [
                    new InlineMark { Type = payload.MarkType },
                    new InlineMark
                    {
                        Type = InlineMarkType.Revision,
                        RevisionId = "rev-1",
                        Value = DocumentRevisionType.Formatting.ToString()
                    }
                ]
            },
            new TextRun { Text = "a" }
        ];
        document.Revisions =
        [
            new DocumentRevision
            {
                Id = "rev-1",
                Type = DocumentRevisionType.Formatting,
                Action = DocumentRevisionAction.Pending,
                Range = new DocumentRevisionRange { BlockId = "a", StartInlineIndex = 1 },
                PayloadJson = JsonSerializer.Serialize(payload, DocumentEditorJson.Options)
            }
        ];
        return document;
    }

    private static string TextOfAllRuns(DocumentEditorDocument document, string blockId)
        => string.Concat(InlinesOf(document, blockId).OfType<TextRun>().Select(run => run.Text));

    private static IReadOnlyList<TextRun> RevisionRunsOf(DocumentEditorDocument document, string blockId)
        => InlinesOf(document, blockId)
            .OfType<TextRun>()
            .Where(run => run.Marks.Any(mark => mark.Type == InlineMarkType.Revision))
            .ToList();

    private static DocumentBlock TableBlock(string id, params (string CellId, string Text)[] cells)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Table,
            Content = new TableBlockContent
            {
                Rows =
                [
                    new TableRowContent
                    {
                        Cells = cells.Select(cell => new TableCellContent
                        {
                            Id = cell.CellId,
                            Blocks =
                            [
                                new DocumentBlock
                                {
                                    Id = $"{cell.CellId}-block",
                                    Type = DocumentBlockType.Paragraph,
                                    Content = new ParagraphBlockContent
                                    {
                                        Inlines = [new TextRun { Text = cell.Text }]
                                    }
                                }
                            ]
                        }).ToList()
                    }
                ]
            }
        };

    private static string CellText(DocumentEditorDocument document, string cellId)
    {
        var table = document.Blocks
            .Select(block => block.Content)
            .OfType<TableBlockContent>()
            .Single();
        var cell = table.Rows.SelectMany(row => row.Cells).Single(cell => cell.Id == cellId);
        return cell.Blocks
            .Select(block => block.Content)
            .OfType<ParagraphBlockContent>()
            .Single()
            .Inlines
            .OfType<TextRun>()
            .Single()
            .Text;
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }
}
