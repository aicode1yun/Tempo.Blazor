using System.Text.RegularExpressions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Creates deterministic word-level diffs for document text.</summary>
public static class DocumentTextDiffHelper
{
    private static readonly Regex TokenRegex = new(@"\p{L}[\p{L}\p{M}\p{N}]*|\p{N}+|[^\s]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Diffs old and new text at word/punctuation level.</summary>
    public static DocumentTextDiffResult Diff(string? oldText, string? newText)
    {
        var oldTokens = Tokenize(oldText);
        var newTokens = Tokenize(newText);
        var table = BuildLcsTable(oldTokens, newTokens);
        var segments = new List<DocumentTextDiffSegment>();

        var oldIndex = 0;
        var newIndex = 0;
        while (oldIndex < oldTokens.Count && newIndex < newTokens.Count)
        {
            if (string.Equals(oldTokens[oldIndex], newTokens[newIndex], StringComparison.Ordinal))
            {
                Append(segments, DocumentTextDiffSegmentKind.Unchanged, oldTokens[oldIndex]);
                oldIndex++;
                newIndex++;
            }
            else if (table[oldIndex + 1, newIndex] >= table[oldIndex, newIndex + 1])
            {
                Append(segments, DocumentTextDiffSegmentKind.Removed, oldTokens[oldIndex]);
                oldIndex++;
            }
            else
            {
                Append(segments, DocumentTextDiffSegmentKind.Added, newTokens[newIndex]);
                newIndex++;
            }
        }

        while (oldIndex < oldTokens.Count)
        {
            Append(segments, DocumentTextDiffSegmentKind.Removed, oldTokens[oldIndex++]);
        }

        while (newIndex < newTokens.Count)
        {
            Append(segments, DocumentTextDiffSegmentKind.Added, newTokens[newIndex++]);
        }

        return new DocumentTextDiffResult { Segments = segments };
    }

    /// <summary>Extracts readable plain text from a document snapshot.</summary>
    public static string ExtractPlainText(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, document.Blocks
            .OrderBy(block => block.Order)
            .Select(GetBlockText)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>Extracts readable plain text from a single document block.</summary>
    public static string ExtractBlockPlainText(DocumentBlock block)
        => GetBlockText(block);

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return TokenRegex.Matches(text)
            .Select(match => match.Value)
            .ToList();
    }

    private static int[,] BuildLcsTable(IReadOnlyList<string> oldTokens, IReadOnlyList<string> newTokens)
    {
        var table = new int[oldTokens.Count + 1, newTokens.Count + 1];
        for (var oldIndex = oldTokens.Count - 1; oldIndex >= 0; oldIndex--)
        {
            for (var newIndex = newTokens.Count - 1; newIndex >= 0; newIndex--)
            {
                table[oldIndex, newIndex] = string.Equals(oldTokens[oldIndex], newTokens[newIndex], StringComparison.Ordinal)
                    ? table[oldIndex + 1, newIndex + 1] + 1
                    : Math.Max(table[oldIndex + 1, newIndex], table[oldIndex, newIndex + 1]);
            }
        }

        return table;
    }

    private static void Append(ICollection<DocumentTextDiffSegment> segments, DocumentTextDiffSegmentKind kind, string token)
    {
        var last = segments.LastOrDefault();
        if (last?.Kind == kind)
        {
            last.Text = JoinTokens(last.Text, token);
            return;
        }

        segments.Add(new DocumentTextDiffSegment
        {
            Kind = kind,
            Text = token
        });
    }

    private static string JoinTokens(string current, string next)
    {
        return NeedsSpace(current, next)
            ? current + " " + next
            : current + next;
    }

    private static bool NeedsSpace(string current, string next)
    {
        if (current.Length == 0 || next.Length == 0)
        {
            return false;
        }

        var previous = current[^1];
        var first = next[0];
        return (char.IsLetterOrDigit(previous) || previous is ')' or ']' or '}' or '.')
            && (char.IsLetterOrDigit(first) || first is '(' or '[' or '{');
    }

    private static string GetBlockText(DocumentBlock block)
    {
        return block.Content switch
        {
            ParagraphBlockContent paragraph => GetInlineText(paragraph.Inlines),
            HeadingBlockContent heading => GetInlineText(heading.Inlines),
            ListBlockContent list => GetInlineText(list.Inlines),
            QuoteBlockContent quote => GetInlineText(quote.Inlines),
            ImageBlockContent image => image.Caption ?? image.AltText ?? image.Url ?? image.AssetId ?? string.Empty,
            PageBreakBlockContent => string.Empty,
            TableBlockContent table => string.Join(" ", table.Rows
                .SelectMany(row => row.Cells)
                .Where(cell => cell.Merge.IsOrigin)
                .SelectMany(cell => cell.Blocks)
                .Select(GetBlockText)
                .Where(text => !string.IsNullOrWhiteSpace(text))),
            _ => string.Empty
        };
    }

    private static string GetInlineText(IEnumerable<InlineContent> inlines)
    {
        return string.Concat(inlines.Select(inline => inline switch
        {
            TextRun run => run.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => note.DisplayMarker ?? note.NoteId,
            _ => string.Empty
        }));
    }
}
