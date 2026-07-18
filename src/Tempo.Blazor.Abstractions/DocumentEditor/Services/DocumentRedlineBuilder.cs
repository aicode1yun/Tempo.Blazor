using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Options for <see cref="DocumentRedlineBuilder"/>.</summary>
public sealed record DocumentRedlineOptions
{
    /// <summary>Author attributed to every generated revision (e.g. "Comparison").</summary>
    public DocumentEditorAuthor Author { get; init; } = new() { Id = "comparison", DisplayName = "Comparison" };

    /// <summary>Timestamp stamped on every generated revision — inject for deterministic output.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Turns a view-only <see cref="DocumentCompareResult"/> into a regular tracked-changes document
/// (a "redline"): word-level diff segments become insertion/deletion revision runs, added/removed
/// blocks become whole-block insertions/deletions (removed blocks are woven back at their base
/// position), and formatting-only changes are recorded as formatting revisions. The output is an
/// ordinary document, so the existing DOCX exporter (w:ins/w:del), the canvas track-changes UI and
/// the PDF pipeline consume it without new model concepts.
/// </summary>
public sealed class DocumentRedlineBuilder
{
    /// <summary>Builds the redline document from a comparison result.</summary>
    public DocumentEditorDocument Build(DocumentCompareResult compareResult, DocumentRedlineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compareResult);
        if (compareResult.CompareDocument is null)
        {
            throw new ArgumentException(
                "DocumentCompareResult.CompareDocument is required to build a redline document.",
                nameof(compareResult));
        }

        options ??= new DocumentRedlineOptions();
        var redline = Clone(compareResult.CompareDocument);
        redline.Revisions ??= [];
        var revisionCounter = 0;
        var changesByBlockId = compareResult.Changes
            .Where(change => !string.IsNullOrWhiteSpace(change.BlockId))
            .ToDictionary(change => change.BlockId!, change => change, StringComparer.Ordinal);

        foreach (var block in redline.Blocks)
        {
            if (!changesByBlockId.TryGetValue(block.Id, out var change))
            {
                continue;
            }

            switch (change.Kind)
            {
                case DocumentCompareChangeKind.Changed when IsFormattingOnly(change):
                    redline.Revisions.Add(CreateRevision(
                        NextRevisionId(block.Id, ref revisionCounter),
                        DocumentRevisionType.Formatting,
                        block.Id,
                        options));
                    break;

                case DocumentCompareChangeKind.Changed when block.Content is ParagraphBlockContent paragraph:
                    paragraph.Inlines = BuildDiffInlines(paragraph.Inlines, block.Id, change.TextDiff, redline, options, ref revisionCounter);
                    break;

                case DocumentCompareChangeKind.Changed when block.Content is HeadingBlockContent heading:
                    heading.Inlines = BuildDiffInlines(heading.Inlines, block.Id, change.TextDiff, redline, options, ref revisionCounter);
                    break;

                case DocumentCompareChangeKind.Added:
                    MarkBlockRuns(block, DocumentRevisionType.Insertion, redline, options, ref revisionCounter);
                    break;
            }
        }

        WeaveRemovedBlocks(compareResult, redline, options, ref revisionCounter);
        NormalizeOrders(redline.Blocks);
        return redline;
    }

    private static List<InlineContent> BuildDiffInlines(
        List<InlineContent> existingInlines,
        string blockId,
        DocumentTextDiffResult textDiff,
        DocumentEditorDocument redline,
        DocumentRedlineOptions options,
        ref int revisionCounter)
    {
        if (textDiff.Segments.Count == 0)
        {
            return existingInlines;
        }

        var inlines = new List<InlineContent>();
        foreach (var segment in textDiff.Segments)
        {
            if (segment.Text.Length == 0)
            {
                continue;
            }

            var run = new TextRun { Text = segment.Text };
            if (segment.Kind != DocumentTextDiffSegmentKind.Unchanged)
            {
                var revision = CreateRevision(
                    NextRevisionId(blockId, ref revisionCounter),
                    segment.Kind == DocumentTextDiffSegmentKind.Added
                        ? DocumentRevisionType.Insertion
                        : DocumentRevisionType.Deletion,
                    blockId,
                    options);
                redline.Revisions.Add(revision);
                run.Marks.Add(new InlineMark { Type = InlineMarkType.Revision, RevisionId = revision.Id });
            }

            inlines.Add(run);
        }

        return inlines;
    }

    private static void WeaveRemovedBlocks(
        DocumentCompareResult compareResult,
        DocumentEditorDocument redline,
        DocumentRedlineOptions options,
        ref int revisionCounter)
    {
        var baseDocument = compareResult.BaseDocument;
        if (baseDocument is null)
        {
            return;
        }

        var removedIds = compareResult.Changes
            .Where(change => change.Kind == DocumentCompareChangeKind.Removed && !string.IsNullOrWhiteSpace(change.BlockId))
            .Select(change => change.BlockId!)
            .ToHashSet(StringComparer.Ordinal);
        if (removedIds.Count == 0)
        {
            return;
        }

        var presentIds = redline.Blocks.Select(block => block.Id).ToHashSet(StringComparer.Ordinal);
        var orderedBase = baseDocument.Blocks.OrderBy(block => block.Order).ToList();
        foreach (var baseBlock in orderedBase)
        {
            if (!removedIds.Contains(baseBlock.Id) || presentIds.Contains(baseBlock.Id))
            {
                continue;
            }

            var woven = Clone(baseBlock);
            MarkBlockRuns(woven, DocumentRevisionType.Deletion, redline, options, ref revisionCounter);

            // Insert after the nearest preceding base block that survived into the compare side.
            var insertAt = 0;
            for (var i = orderedBase.IndexOf(baseBlock) - 1; i >= 0; i--)
            {
                var predecessorIndex = redline.Blocks.FindIndex(block => block.Id == orderedBase[i].Id);
                if (predecessorIndex >= 0)
                {
                    insertAt = predecessorIndex + 1;
                    break;
                }
            }

            redline.Blocks.Insert(insertAt, woven);
            presentIds.Add(woven.Id);
        }
    }

    private static void MarkBlockRuns(
        DocumentBlock block,
        DocumentRevisionType type,
        DocumentEditorDocument redline,
        DocumentRedlineOptions options,
        ref int revisionCounter)
    {
        var blockRevision = CreateRevision(NextRevisionId(block.Id, ref revisionCounter), type, block.Id, options);
        redline.Revisions.Add(blockRevision);

        foreach (var run in EnumerateTextRuns(block))
        {
            if (run.Marks.Any(mark => mark.Type == InlineMarkType.Revision))
            {
                continue;
            }

            run.Marks.Add(new InlineMark { Type = InlineMarkType.Revision, RevisionId = blockRevision.Id });
        }
    }

    private static IEnumerable<TextRun> EnumerateTextRuns(DocumentBlock block)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent paragraph:
                foreach (var run in paragraph.Inlines.OfType<TextRun>())
                {
                    yield return run;
                }

                break;
            case HeadingBlockContent heading:
                foreach (var run in heading.Inlines.OfType<TextRun>())
                {
                    yield return run;
                }

                break;
            case ListBlockContent list:
                foreach (var run in list.Inlines.OfType<TextRun>())
                {
                    yield return run;
                }

                break;
            case QuoteBlockContent quote:
                foreach (var run in quote.Inlines.OfType<TextRun>())
                {
                    yield return run;
                }

                break;
            case TableBlockContent table:
                foreach (var run in table.Rows
                             .SelectMany(row => row.Cells)
                             .SelectMany(cell => cell.Blocks)
                             .SelectMany(EnumerateTextRuns))
                {
                    yield return run;
                }

                break;
        }
    }

    private static bool IsFormattingOnly(DocumentCompareBlockChange change)
        => change.TextDiff.Segments.Count > 0
           && change.TextDiff.Segments.All(segment => segment.Kind == DocumentTextDiffSegmentKind.Unchanged);

    private static DocumentRevision CreateRevision(
        string id,
        DocumentRevisionType type,
        string blockId,
        DocumentRedlineOptions options)
        => new()
        {
            Id = id,
            Type = type,
            Range = new DocumentRevisionRange { BlockId = blockId },
            Author = new DocumentRevisionAuthor
            {
                Id = options.Author.Id,
                DisplayName = options.Author.DisplayName,
            },
            CreatedAt = options.Timestamp,
        };

    // Deterministic ids: same compare result + options → byte-identical redline (testable, cacheable).
    private static string NextRevisionId(string blockId, ref int revisionCounter)
        => $"redline-{blockId}-{++revisionCounter}";

    private static void NormalizeOrders(List<DocumentBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            blocks[i].Order = i + 1;
        }
    }

    private static T Clone<T>(T value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, DocumentEditorJson.Options), DocumentEditorJson.Options)
           ?? throw new JsonException("Could not clone document for redline building.");
}
