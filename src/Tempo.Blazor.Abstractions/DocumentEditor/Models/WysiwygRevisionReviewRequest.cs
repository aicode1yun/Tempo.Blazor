namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Inline revision review request raised by the WYSIWYG surface.</summary>
public sealed class WysiwygRevisionReviewRequest
{
    /// <summary>Revision id requested by the inline review UI.</summary>
    public string RevisionId { get; set; } = string.Empty;

    /// <summary>Requested review action.</summary>
    public DocumentRevisionAction Action { get; set; } = DocumentRevisionAction.Pending;
}
