using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Payload emitted when a document comment thread requests page navigation.</summary>
public sealed class DocumentCommentThreadNavigateRequest
{
    /// <summary>Target thread identifier.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>One-based page number requested by the thread.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Thread anchor used for the navigation request.</summary>
    public DocumentCommentAnchor Anchor { get; set; } = DocumentCommentAnchor.Page(1);
}
