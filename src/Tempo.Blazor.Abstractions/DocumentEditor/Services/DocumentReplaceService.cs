using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Performs find-and-replace operations on a <see cref="DocumentEditorDocument"/>.</summary>
public sealed class DocumentReplaceService
{
    private readonly DocumentSearchService _search = new();

    /// <summary>Replaces the single occurrence identified by <paramref name="result"/> with <paramref name="replacement"/>.</summary>
    public void ReplaceOne(DocumentEditorDocument document, DocumentSearchResult result, string replacement)
    {
        var block = FindBlock(document, result.BlockId);
        if (block is null) return;
        ApplyReplacement(block, result.BlockTextOffset, result.Length, replacement);
    }

    /// <summary>Replaces all occurrences matching <paramref name="query"/> with <paramref name="replacement"/>.
    /// Returns the number of replacements made.</summary>
    public int ReplaceAll(DocumentEditorDocument document, DocumentSearchQuery query, string replacement)
    {
        // Search first, then apply replacements in reverse order per block so offsets stay valid.
        var results = _search.Search(document, query);
        if (results.Count == 0) return 0;

        // Group by block, apply in reverse offset order within each block
        var byBlock = results.GroupBy(r => r.BlockId);
        var count = 0;

        foreach (var group in byBlock)
        {
            var block = FindBlock(document, group.Key);
            if (block is null) continue;

            foreach (var result in group.OrderByDescending(r => r.BlockTextOffset))
            {
                ApplyReplacement(block, result.BlockTextOffset, result.Length, replacement);
                count++;
            }
        }

        return count;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static DocumentBlock? FindBlock(DocumentEditorDocument document, string blockId)
    {
        var block = FindBlockInList(document.Blocks, blockId);
        if (block is not null)
        {
            return block;
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            block = FindBlockInList(headerFooter.Blocks, blockId);
            if (block is not null)
            {
                return block;
            }
        }

        return null;
    }

    private static DocumentBlock? FindBlockInList(IEnumerable<DocumentBlock> blocks, string blockId)
    {
        foreach (var block in blocks)
        {
            if (block.Id == blockId) return block;

            if (block.Content is TableBlockContent table)
            {
                foreach (var row in table.Rows)
                    foreach (var cell in row.Cells)
                    {
                        var found = FindBlockInList(cell.Blocks, blockId);
                        if (found is not null) return found;
                    }
            }

            if (block.Content is ContentControlBlockContent contentControl)
            {
                var found = FindBlockInList(contentControl.Blocks, blockId);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static void ApplyReplacement(DocumentBlock block, int offset, int length, string replacement)
    {
        var inlines = GetInlines(block);
        if (inlines is null) return;

        // Build a flat map: (inline index, char start, char end)
        var segments = BuildSegments(inlines);
        var matchEnd = offset + length;

        // Find all inlines that overlap with [offset, matchEnd)
        var overlapping = segments
            .Where(s => s.End > offset && s.Start < matchEnd)
            .ToList();

        if (overlapping.Count == 0) return;

        // Inherit marks from the first overlapping inline
        var inheritedMarks = overlapping[0].Run.Marks.ToList();

        // Build new inline list
        var newInlines = new List<InlineContent>();
        var processed = new HashSet<int>();

        foreach (var seg in segments)
        {
            if (seg.End <= offset || seg.Start >= matchEnd)
            {
                // Outside match — keep as-is
                newInlines.Add(seg.Run);
                continue;
            }

            // This segment overlaps the match — handle once
            if (processed.Count > 0) continue;
            processed.Add(seg.Index);

            // Text before match (within this first overlapping run)
            var beforeLen = Math.Max(0, offset - seg.Start);
            if (beforeLen > 0)
            {
                newInlines.Add(Clone(seg.Run, seg.Run.Text[..beforeLen]));
            }

            // Replacement run with inherited marks
            if (replacement.Length > 0)
            {
                newInlines.Add(new TextRun
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = replacement,
                    Marks = [.. inheritedMarks]
                });
            }

            // Text after match in the LAST overlapping run
            var lastSeg = overlapping[^1];
            var afterStart = matchEnd - lastSeg.Start;
            if (afterStart < lastSeg.Run.Text.Length)
            {
                newInlines.Add(Clone(lastSeg.Run, lastSeg.Run.Text[afterStart..]));
            }

            // Mark all overlapping as consumed
            foreach (var o in overlapping) processed.Add(o.Index);
        }

        SetInlines(block, newInlines);
    }

    private static List<(int Index, int Start, int End, TextRun Run)> BuildSegments(
        IList<InlineContent> inlines)
    {
        var result = new List<(int, int, int, TextRun)>();
        var pos = 0;
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is TextRun run)
            {
                result.Add((i, pos, pos + run.Text.Length, run));
                pos += run.Text.Length;
            }
        }
        return result;
    }

    private static TextRun Clone(TextRun source, string newText) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Text = newText,
        Marks = [.. source.Marks]
    };

    private static IList<InlineContent>? GetInlines(DocumentBlock block) => block.Content switch
    {
        ParagraphBlockContent p => p.Inlines,
        HeadingBlockContent h => h.Inlines,
        ListBlockContent l => l.Inlines,
        QuoteBlockContent q => q.Inlines,
        _ => null
    };

    private static void SetInlines(DocumentBlock block, List<InlineContent> inlines)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent p: p.Inlines = inlines; break;
            case HeadingBlockContent h: h.Inlines = inlines; break;
            case ListBlockContent l: l.Inlines = inlines; break;
            case QuoteBlockContent q: q.Inlines = inlines; break;
        }
    }
}
