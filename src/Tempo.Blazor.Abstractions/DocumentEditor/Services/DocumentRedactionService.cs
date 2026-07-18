using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>
/// Applies redaction marks with REAL content removal: <see cref="Apply"/> returns an export copy
/// of the document where every run carrying <see cref="InlineMarkType.Redaction"/> has its text
/// replaced with block characters (█). The original characters no longer exist in the canonical
/// model, so no export format (DOCX XML, PDF text layer, HTML, Markdown) can leak them — a visual
/// overlay alone would keep the content recoverable. Covers body blocks, table cells, content
/// controls and headers/footers. The source document is never mutated.
/// </summary>
public static class DocumentRedactionService
{
    private const char BlockCharacter = '█';

    /// <summary>Returns an export copy with all redacted run text destructively replaced.</summary>
    public static DocumentEditorDocument Apply(DocumentEditorDocument document)
    {
        var copy = Clone(document);
        foreach (var block in AllBlocks(copy))
        {
            RedactBlock(block);
        }

        return copy;
    }

    /// <summary>Whether the document contains any redaction mark (body, tables, headers/footers).</summary>
    public static bool HasRedactions(DocumentEditorDocument? document)
        => document is not null
           && AllBlocks(document).Any(block => Inlines(block).Any(IsRedacted));

    private static void RedactBlock(DocumentBlock block)
    {
        foreach (var inline in Inlines(block))
        {
            if (inline is TextRun run && IsRedacted(run))
            {
                run.Text = new string(BlockCharacter, run.Text.Length);
            }
        }
    }

    private static bool IsRedacted(InlineContent inline)
        => inline is TextRun run && run.Marks.Any(mark => mark.Type == InlineMarkType.Redaction);

    private static IEnumerable<DocumentBlock> AllBlocks(DocumentEditorDocument document)
    {
        foreach (var block in document.Blocks
                     .Concat(document.HeadersFooters.SelectMany(part => part.Blocks))
                     .Concat(document.Notes.SelectMany(note => note.Blocks)))
        {
            foreach (var nested in WithNestedBlocks(block))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<DocumentBlock> WithNestedBlocks(DocumentBlock block)
    {
        yield return block;
        if (block.Content is TableBlockContent table)
        {
            foreach (var nested in table.Rows
                         .SelectMany(row => row.Cells)
                         .SelectMany(cell => cell.Blocks)
                         .SelectMany(WithNestedBlocks))
            {
                yield return nested;
            }
        }

        if (block.Content is ContentControlBlockContent control)
        {
            foreach (var nested in control.Blocks.SelectMany(WithNestedBlocks))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<InlineContent> Inlines(DocumentBlock block)
        => block.Content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => []
        };

    private static T Clone<T>(T value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, DocumentEditorJson.Options), DocumentEditorJson.Options)
           ?? throw new JsonException("Could not clone document for redaction.");
}
