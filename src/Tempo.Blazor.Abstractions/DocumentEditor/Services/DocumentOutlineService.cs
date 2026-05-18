using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>A single entry in the document heading outline.</summary>
public sealed record DocumentOutlineItem(string BlockId, int Level, string Text);

/// <summary>Extracts a navigable heading outline from a document.</summary>
public sealed class DocumentOutlineService
{
    public IReadOnlyList<DocumentOutlineItem> GetOutline(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var items = new List<DocumentOutlineItem>();
        foreach (var block in document.Blocks)
        {
            if (block.Content is not HeadingBlockContent heading) continue;
            var text = ExtractText(heading.Inlines);
            items.Add(new DocumentOutlineItem(block.Id, heading.Level, text));
        }
        return items;
    }

    private static string ExtractText(IEnumerable<InlineContent> inlines)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            if (inline is TextRun run) sb.Append(run.Text);
            else if (inline is TokenRun token) sb.Append(token.DisplayName);
        }
        return sb.ToString();
    }
}
