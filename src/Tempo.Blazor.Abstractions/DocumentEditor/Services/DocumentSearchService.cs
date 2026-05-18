using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Searches a <see cref="DocumentEditorDocument"/> for text matches.</summary>
public sealed class DocumentSearchService
{
    /// <summary>Finds all occurrences of <paramref name="query"/> in <paramref name="document"/>.</summary>
    public IReadOnlyList<DocumentSearchResult> Search(DocumentEditorDocument document, DocumentSearchQuery query)
    {
        if (string.IsNullOrEmpty(query.Text))
            return [];

        var results = new List<DocumentSearchResult>();

        if (query.Scope is DocumentSearchScope.Body or DocumentSearchScope.All)
            CollectFromBlocks(document.Blocks, query, DocumentSearchScope.Body, results);

        if (query.Scope is DocumentSearchScope.HeadersFooters or DocumentSearchScope.All)
        {
            foreach (var hf in document.HeadersFooters)
                CollectFromBlocks(hf.Blocks, query, DocumentSearchScope.HeadersFooters, results);
        }

        if (query.Scope is DocumentSearchScope.Comments or DocumentSearchScope.All)
            CollectFromComments(document.Comments, query, results);

        // Re-index globally
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            results[i] = new DocumentSearchResult
            {
                Index = i,
                BlockId = r.BlockId,
                BlockTextOffset = r.BlockTextOffset,
                Length = r.Length,
                Scope = r.Scope,
                MarkerId = BuildMarkerId(i, r),
                Preview = r.Preview
            };
        }

        return results;
    }

    private static void CollectFromBlocks(
        IEnumerable<DocumentBlock> blocks,
        DocumentSearchQuery query,
        DocumentSearchScope resultScope,
        List<DocumentSearchResult> results)
    {
        foreach (var block in blocks)
            CollectFromBlock(block, query, resultScope, results);
    }

    private static void CollectFromBlock(
        DocumentBlock block,
        DocumentSearchQuery query,
        DocumentSearchScope resultScope,
        List<DocumentSearchResult> results)
    {
        switch (block.Content)
        {
            case ParagraphBlockContent p:
                CollectFromInlines(block.Id, p.Inlines, query, resultScope, results);
                break;

            case HeadingBlockContent h:
                CollectFromInlines(block.Id, h.Inlines, query, resultScope, results);
                break;

            case ListBlockContent l:
                CollectFromInlines(block.Id, l.Inlines, query, resultScope, results);
                break;

            case QuoteBlockContent q:
                CollectFromInlines(block.Id, q.Inlines, query, resultScope, results);
                break;

            case TableBlockContent t:
                foreach (var row in t.Rows)
                    foreach (var cell in row.Cells)
                        CollectFromBlocks(cell.Blocks, query, resultScope, results);
                break;
        }
    }

    private static void CollectFromInlines(
        string blockId,
        IEnumerable<InlineContent> inlines,
        DocumentSearchQuery query,
        DocumentSearchScope resultScope,
        List<DocumentSearchResult> results)
    {
        // Flatten all text runs into a single string, keeping offset map
        var flatText = string.Concat(
            inlines.OfType<TextRun>().Select(r => r.Text));

        if (string.IsNullOrEmpty(flatText))
            return;

        var comparison = query.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var searchText = query.Text;
        var pos = 0;

        while (pos <= flatText.Length - searchText.Length)
        {
            var idx = flatText.IndexOf(searchText, pos, comparison);
            if (idx < 0) break;

            if (!query.WholeWord || IsWholeWordMatch(flatText, idx, searchText.Length))
            {
                var matchText = flatText.Substring(idx, searchText.Length);
                results.Add(new DocumentSearchResult
                {
                    Index = results.Count, // temporary; re-indexed after all blocks
                    BlockId = blockId,
                    BlockTextOffset = idx,
                    Length = searchText.Length,
                    Scope = resultScope,
                    Preview = matchText.Length <= 80 ? matchText : matchText[..80]
                });
            }

            pos = idx + 1;
        }
    }

    private static bool IsWholeWordMatch(string text, int start, int length)
    {
        var before = start > 0 ? text[start - 1] : ' ';
        var after = start + length < text.Length ? text[start + length] : ' ';
        return !char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after);
    }

    private static void CollectFromComments(
        IEnumerable<DocumentComment> comments,
        DocumentSearchQuery query,
        List<DocumentSearchResult> results)
    {
        foreach (var comment in comments)
        {
            foreach (var entry in comment.Entries)
            {
                CollectFromPlainText(
                    $"comment:{comment.Id}:{entry.Id}",
                    entry.Text,
                    query,
                    DocumentSearchScope.Comments,
                    results);
            }
        }
    }

    private static void CollectFromPlainText(
        string blockId,
        string text,
        DocumentSearchQuery query,
        DocumentSearchScope resultScope,
        List<DocumentSearchResult> results)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var comparison = query.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var pos = 0;
        while (pos <= text.Length - query.Text.Length)
        {
            var idx = text.IndexOf(query.Text, pos, comparison);
            if (idx < 0) break;

            if (!query.WholeWord || IsWholeWordMatch(text, idx, query.Text.Length))
            {
                var matchText = text.Substring(idx, query.Text.Length);
                results.Add(new DocumentSearchResult
                {
                    Index = results.Count,
                    BlockId = blockId,
                    BlockTextOffset = idx,
                    Length = query.Text.Length,
                    Scope = resultScope,
                    Preview = matchText.Length <= 80 ? matchText : matchText[..80]
                });
            }

            pos = idx + 1;
        }
    }

    private static string BuildMarkerId(int index, DocumentSearchResult result) =>
        $"search-{index}-{result.Scope}-{result.BlockId}-{result.BlockTextOffset}-{result.Length}";
}
