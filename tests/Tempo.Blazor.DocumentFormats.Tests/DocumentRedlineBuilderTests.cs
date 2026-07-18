using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.DocumentFormats.Redline;

namespace Tempo.Blazor.DocumentFormats.Tests;

/// <summary>
/// Specification of the diff → tracked-changes mapping: a view-only DocumentCompareResult becomes a
/// regular document whose changes are expressed as DocumentRevision entries + Revision inline marks,
/// so the existing DOCX exporter (w:ins/w:del), the canvas track-changes UI, and the PDF pipeline
/// all consume it without new model concepts.
/// </summary>
public class DocumentRedlineBuilderTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    // ── Changed block: word-level ins/del runs ──────────────────────────────────────────────────

    [Fact]
    public void Build_ChangedBlock_MapsDiffSegmentsToInsertionAndDeletionRuns()
    {
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "Cena je 100 Kč")],
            compareBlocks: [Paragraph("b1", "Cena je 200 Kč")],
            changes:
            [
                Changed("b1", "Cena je 100 Kč", "Cena je 200 Kč",
                    Segment(DocumentTextDiffSegmentKind.Unchanged, "Cena je "),
                    Segment(DocumentTextDiffSegmentKind.Removed, "100"),
                    Segment(DocumentTextDiffSegmentKind.Added, "200"),
                    Segment(DocumentTextDiffSegmentKind.Unchanged, " Kč")),
            ]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        var block = redline.Blocks.Single(candidate => candidate.Id == "b1");
        var runs = ((ParagraphBlockContent)block.Content).Inlines.OfType<TextRun>().ToList();
        runs.Select(run => run.Text).Should().Equal("Cena je ", "100", "200", " Kč");

        RevisionIdOf(runs[0]).Should().BeNull("unchanged text carries no revision");
        var deletion = RevisionOf(redline, runs[1]);
        deletion.Type.Should().Be(DocumentRevisionType.Deletion);
        var insertion = RevisionOf(redline, runs[2]);
        insertion.Type.Should().Be(DocumentRevisionType.Insertion);

        deletion.Author.DisplayName.Should().Be("Porovnání");
        deletion.CreatedAt.Should().Be(Timestamp);
        deletion.Range.BlockId.Should().Be("b1");
    }

    [Fact]
    public void Build_ChangedHeading_MapsDiffSegmentsToRevisionRuns()
    {
        var baseHeading = Heading("h1", "Smlouva o dílo");
        var compareHeading = Heading("h1", "Smlouva o půjčce");
        var result = CompareResult(
            baseBlocks: [baseHeading],
            compareBlocks: [compareHeading],
            changes:
            [
                Changed("h1", "Smlouva o dílo", "Smlouva o půjčce",
                    Segment(DocumentTextDiffSegmentKind.Unchanged, "Smlouva o "),
                    Segment(DocumentTextDiffSegmentKind.Removed, "dílo"),
                    Segment(DocumentTextDiffSegmentKind.Added, "půjčce")),
            ]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        var block = redline.Blocks.Single(candidate => candidate.Id == "h1");
        var runs = ((HeadingBlockContent)block.Content).Inlines.OfType<TextRun>().ToList();
        runs.Select(run => run.Text).Should().Equal("Smlouva o ", "dílo", "půjčce");
        RevisionOf(redline, runs[1]).Type.Should().Be(DocumentRevisionType.Deletion);
        RevisionOf(redline, runs[2]).Type.Should().Be(DocumentRevisionType.Insertion);
    }

    private static DocumentBlock Heading(string id, string text)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Heading,
            Content = new HeadingBlockContent { Level = 1, Inlines = [new TextRun { Text = text }] },
        };

    // ── Added / removed blocks ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_AddedBlock_MarksAllRunsAsInsertion()
    {
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "První odstavec.")],
            compareBlocks: [Paragraph("b1", "První odstavec."), Paragraph("b2", "Nový odstavec.")],
            changes: [new DocumentCompareBlockChange { Kind = DocumentCompareChangeKind.Added, BlockId = "b2", NewText = "Nový odstavec." }]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        var added = redline.Blocks.Single(candidate => candidate.Id == "b2");
        var run = ((ParagraphBlockContent)added.Content).Inlines.OfType<TextRun>().Single();
        RevisionOf(redline, run).Type.Should().Be(DocumentRevisionType.Insertion);
    }

    [Fact]
    public void Build_RemovedBlock_IsWovenBackAtItsBasePositionWithDeletionRuns()
    {
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "Zůstává."), Paragraph("b2", "Bude smazán."), Paragraph("b3", "Také zůstává.")],
            compareBlocks: [Paragraph("b1", "Zůstává."), Paragraph("b3", "Také zůstává.")],
            changes: [new DocumentCompareBlockChange { Kind = DocumentCompareChangeKind.Removed, BlockId = "b2", OldText = "Bude smazán." }]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        redline.Blocks.Select(block => block.Id).Should().Equal(["b1", "b2", "b3"], "the deleted block keeps its base position");
        redline.Blocks.Select(block => block.Order).Should().BeInAscendingOrder();
        var removedRun = ((ParagraphBlockContent)redline.Blocks[1].Content).Inlines.OfType<TextRun>().Single();
        RevisionOf(redline, removedRun).Type.Should().Be(DocumentRevisionType.Deletion);
    }

    // ── Formatting-only change ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_FormattingOnlyChange_KeepsNewInlinesAndRecordsFormattingRevision()
    {
        var boldParagraph = Paragraph("b1", "Stejný text");
        ((ParagraphBlockContent)boldParagraph.Content).Inlines.OfType<TextRun>().Single().Marks.Add(new InlineMark { Type = InlineMarkType.Bold });
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "Stejný text")],
            compareBlocks: [boldParagraph],
            changes:
            [
                Changed("b1", "Stejný text", "Stejný text",
                    Segment(DocumentTextDiffSegmentKind.Unchanged, "Stejný text")),
            ]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        var block = redline.Blocks.Single(candidate => candidate.Id == "b1");
        var run = ((ParagraphBlockContent)block.Content).Inlines.OfType<TextRun>().Single();
        run.Marks.Should().Contain(mark => mark.Type == InlineMarkType.Bold, "new-side formatting wins");
        redline.Revisions.Should().Contain(revision =>
            revision.Type == DocumentRevisionType.Formatting && revision.Range.BlockId == "b1");
    }

    // ── Tables and images (block-level) ─────────────────────────────────────────────────────────

    [Fact]
    public void Build_RemovedTable_MarksCellRunsAsDeletion()
    {
        var table = TableBlock("t1", "Buňka A", "Buňka B");
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "Text."), table],
            compareBlocks: [Paragraph("b1", "Text.")],
            changes: [new DocumentCompareBlockChange { Kind = DocumentCompareChangeKind.Removed, BlockId = "t1", OldText = "Buňka A | Buňka B" }]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        var woven = redline.Blocks.Single(candidate => candidate.Id == "t1");
        var cellRuns = ((TableBlockContent)woven.Content).Rows
            .SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Blocks)
            .SelectMany(cellBlock => ((ParagraphBlockContent)cellBlock.Content).Inlines.OfType<TextRun>())
            .ToList();
        cellRuns.Should().NotBeEmpty();
        cellRuns.Should().OnlyContain(run => RevisionIdOf(run) != null, "every table cell run is part of the deletion");
        redline.Revisions.Should().Contain(revision => revision.Type == DocumentRevisionType.Deletion && revision.Range.BlockId == "t1");
    }

    [Fact]
    public void Build_AddedImageBlock_RecordsImageInsertionRevision()
    {
        var image = new DocumentBlock
        {
            Id = "img1",
            Type = DocumentBlockType.Image,
            Order = 2,
            Content = new ImageBlockContent { Url = "https://example.test/logo.png", AltText = "Logo" },
        };
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "Text.")],
            compareBlocks: [Paragraph("b1", "Text."), image],
            changes: [new DocumentCompareBlockChange { Kind = DocumentCompareChangeKind.Added, BlockId = "img1", NewText = "[Image] Logo" }]);

        var redline = new DocumentRedlineBuilder().Build(result, Options());

        redline.Blocks.Should().Contain(block => block.Id == "img1");
        redline.Revisions.Should().Contain(revision =>
            revision.Range.BlockId == "img1" && revision.Type == DocumentRevisionType.Insertion);
    }

    // ── Contract guards ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithoutCompareDocument_Throws()
    {
        var act = () => new DocumentRedlineBuilder().Build(new DocumentCompareResult { CompareDocument = null }, Options());

        act.Should().Throw<ArgumentException>().WithMessage("*CompareDocument*");
    }

    [Fact]
    public void Build_IsDeterministic_ForIdenticalInputs()
    {
        var result = CompareResult(
            baseBlocks: [Paragraph("b1", "Cena je 100 Kč")],
            compareBlocks: [Paragraph("b1", "Cena je 200 Kč")],
            changes:
            [
                Changed("b1", "Cena je 100 Kč", "Cena je 200 Kč",
                    Segment(DocumentTextDiffSegmentKind.Unchanged, "Cena je "),
                    Segment(DocumentTextDiffSegmentKind.Removed, "100"),
                    Segment(DocumentTextDiffSegmentKind.Added, "200"),
                    Segment(DocumentTextDiffSegmentKind.Unchanged, " Kč")),
            ]);

        var first = new DocumentRedlineBuilder().Build(result, Options());
        var second = new DocumentRedlineBuilder().Build(result, Options());

        DocumentEditorJson.Serialize(first).Should().Be(DocumentEditorJson.Serialize(second));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static DocumentRedlineOptions Options()
        => new()
        {
            Author = new DocumentEditorAuthor { Id = "compare", DisplayName = "Porovnání" },
            Timestamp = Timestamp,
        };

    private static DocumentCompareResult CompareResult(
        List<DocumentBlock> baseBlocks,
        List<DocumentBlock> compareBlocks,
        List<DocumentCompareBlockChange> changes)
    {
        var baseDocument = DocumentEditorDocument.Empty();
        baseDocument.DocumentId = "compare-doc";
        baseDocument.Blocks = baseBlocks;
        NormalizeOrders(baseDocument.Blocks);
        var compareDocument = DocumentEditorDocument.Empty();
        compareDocument.DocumentId = "compare-doc";
        compareDocument.Blocks = compareBlocks;
        NormalizeOrders(compareDocument.Blocks);
        return new DocumentCompareResult
        {
            Success = true,
            BaseDocument = baseDocument,
            CompareDocument = compareDocument,
            Changes = changes,
        };
    }

    private static void NormalizeOrders(List<DocumentBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            blocks[i].Order = i + 1;
        }
    }

    private static DocumentBlock Paragraph(string id, string text)
        => new()
        {
            Id = id,
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent { Inlines = [new TextRun { Text = text }] },
        };

    private static DocumentBlock TableBlock(string id, params string[] cellTexts)
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
                        Cells = cellTexts
                            .Select((text, index) => new TableCellContent
                            {
                                Blocks = [Paragraph($"{id}-cell-{index}", text)],
                            })
                            .ToList(),
                    },
                ],
            },
        };

    private static DocumentCompareBlockChange Changed(string blockId, string oldText, string newText, params DocumentTextDiffSegment[] segments)
        => new()
        {
            Kind = DocumentCompareChangeKind.Changed,
            BlockId = blockId,
            OldText = oldText,
            NewText = newText,
            TextDiff = new DocumentTextDiffResult { Segments = segments.ToList() },
        };

    private static DocumentTextDiffSegment Segment(DocumentTextDiffSegmentKind kind, string text)
        => new() { Kind = kind, Text = text };

    private static string? RevisionIdOf(TextRun run)
        => run.Marks.FirstOrDefault(mark => mark.Type == InlineMarkType.Revision)?.RevisionId;

    private static DocumentRevision RevisionOf(DocumentEditorDocument document, TextRun run)
    {
        var revisionId = RevisionIdOf(run);
        revisionId.Should().NotBeNull();
        return document.Revisions.Single(revision => revision.Id == revisionId);
    }
}
