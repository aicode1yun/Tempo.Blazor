using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>Read-only queries over a document used by the editor UI.</summary>
public static class DocumentInspection
{
    /// <summary>
    /// Returns whether the document contains any non-empty raw block. The editor surfaces this as a
    /// warning because raw content is emitted verbatim and bypasses sanitization.
    /// </summary>
    public static bool ContainsRawContent(this EmailTemplateDocument document)
        => DocumentTree.AllBlocks(document).OfType<EmailRawBlock>().Any(r => !string.IsNullOrEmpty(r.Content));
}
