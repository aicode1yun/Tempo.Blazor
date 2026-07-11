using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Files;

/// <summary>
/// Describes a text selection captured from the PDF text layer: the selected text,
/// the one-based page it belongs to, and the normalized highlight rectangles covering it.
/// </summary>
/// <param name="Text">The selected text.</param>
/// <param name="Page">One-based page number the selection belongs to.</param>
/// <param name="Rects">Normalized highlight rectangles covering the selection.</param>
public sealed record PdfTextSelection(string Text, int Page, IReadOnlyList<DocumentCommentRect> Rects)
{
    /// <summary>Returns true when the selection has text and at least one valid rectangle.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Text) && Rects.Count > 0 && Rects.All(r => r.IsValid);

    /// <summary>Builds a text range anchor from this selection.</summary>
    public DocumentCommentAnchor ToAnchor() => DocumentCommentAnchor.TextRange(Page, Rects, Text);
}
