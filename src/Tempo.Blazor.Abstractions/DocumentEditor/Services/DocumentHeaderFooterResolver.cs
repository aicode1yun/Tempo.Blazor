using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Locates and mutates header/footer definitions within a document.</summary>
public static class DocumentHeaderFooterResolver
{
    /// <summary>Finds a header or footer by its stable id.</summary>
    public static DocumentHeaderFooter? FindById(DocumentEditorDocument document, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return document.HeadersFooters.FirstOrDefault(hf =>
            string.Equals(hf.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures the document has at least one primary header and one primary footer definition,
    /// creating empty defaults when they are missing. Also ensures every section has references
    /// to the primary header and footer.
    /// </summary>
    public static void EnsurePrimaryHeadersFooters(DocumentEditorDocument document)
    {
        var primaryHeader = document.HeadersFooters
            .FirstOrDefault(hf => hf.Type == DocumentHeaderFooterType.Header
                               && hf.Scope == DocumentHeaderFooterScope.Primary);
        if (primaryHeader is null)
        {
            primaryHeader = new DocumentHeaderFooter
            {
                Type = DocumentHeaderFooterType.Header,
                Scope = DocumentHeaderFooterScope.Primary
            };
            document.HeadersFooters.Add(primaryHeader);
        }

        var primaryFooter = document.HeadersFooters
            .FirstOrDefault(hf => hf.Type == DocumentHeaderFooterType.Footer
                               && hf.Scope == DocumentHeaderFooterScope.Primary);
        if (primaryFooter is null)
        {
            primaryFooter = new DocumentHeaderFooter
            {
                Type = DocumentHeaderFooterType.Footer,
                Scope = DocumentHeaderFooterScope.Primary
            };
            document.HeadersFooters.Add(primaryFooter);
        }

        foreach (var section in document.Sections)
        {
            EnsureSectionReference(section, primaryHeader.Id, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.Primary);
            EnsureSectionReference(section, primaryFooter.Id, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.Primary);
        }
    }

    /// <summary>Enables or disables the different-first-page header/footer setting for all sections.</summary>
    public static void SetDifferentFirstPage(DocumentEditorDocument document, bool enabled)
    {
        foreach (var section in document.Sections)
        {
            section.Properties.DifferentFirstPage = enabled;

            if (enabled)
            {
                EnsureFirstPageHeadersFooters(document, section);
            }
        }
    }

    /// <summary>Enables or disables the different-odd-and-even-pages header/footer setting for all sections.</summary>
    public static void SetDifferentOddAndEvenPages(DocumentEditorDocument document, bool enabled)
    {
        foreach (var section in document.Sections)
        {
            section.Properties.DifferentOddAndEvenPages = enabled;

            if (enabled)
            {
                EnsureEvenPageHeadersFooters(document, section);
            }
        }
    }

    private static void EnsureSectionReference(
        DocumentSection section,
        string headerId,
        DocumentHeaderFooterType type,
        DocumentHeaderFooterScope scope)
    {
        var existing = section.Properties.HeaderFooterReferences
            .FirstOrDefault(r => r.Type == type && r.Scope == scope);
        if (existing is null)
        {
            section.Properties.HeaderFooterReferences.Add(new DocumentHeaderFooterReference
            {
                HeaderFooterId = headerId,
                Type = type,
                Scope = scope
            });
        }
    }

    private static void EnsureFirstPageHeadersFooters(DocumentEditorDocument document, DocumentSection section)
    {
        EnsureAndReferenceHeaderFooter(document, section, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.FirstPage);
        EnsureAndReferenceHeaderFooter(document, section, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.FirstPage);
    }

    private static void EnsureEvenPageHeadersFooters(DocumentEditorDocument document, DocumentSection section)
    {
        EnsureAndReferenceHeaderFooter(document, section, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.EvenPages);
        EnsureAndReferenceHeaderFooter(document, section, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.EvenPages);
    }

    private static void EnsureAndReferenceHeaderFooter(
        DocumentEditorDocument document,
        DocumentSection section,
        DocumentHeaderFooterType type,
        DocumentHeaderFooterScope scope)
    {
        var existing = document.HeadersFooters
            .FirstOrDefault(hf => hf.Type == type && hf.Scope == scope && hf.SectionId == section.Id);
        if (existing is null)
        {
            existing = new DocumentHeaderFooter
            {
                Type = type,
                Scope = scope,
                SectionId = section.Id
            };
            document.HeadersFooters.Add(existing);
        }

        EnsureSectionReference(section, existing.Id, type, scope);
    }
}
