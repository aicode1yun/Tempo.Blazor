namespace Tempo.Blazor.DocumentEditor.Services;

using Tempo.Blazor.DocumentEditor.Models;

/// <summary>Resolves and creates section-scoped document headers and footers.</summary>
public static class DocumentHeaderFooterResolver
{
    /// <summary>Ensures every document section has editable primary header and footer definitions.</summary>
    public static void EnsurePrimaryHeadersFooters(DocumentEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Sections ??= [];
        document.HeadersFooters ??= [];

        if (document.Sections.Count == 0)
        {
            document.Sections.Add(new DocumentSection { Order = 0 });
        }

        foreach (var section in document.Sections)
        {
            section.Properties ??= new DocumentSectionProperties();
            Ensure(document, section, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.Primary);
            Ensure(document, section, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.Primary);
        }
    }

    /// <summary>Sets different first-page mode and creates first-page header/footer targets when enabled.</summary>
    public static void SetDifferentFirstPage(DocumentEditorDocument document, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsurePrimaryHeadersFooters(document);

        foreach (var section in document.Sections)
        {
            section.Properties.DifferentFirstPage = enabled;
            if (!enabled)
            {
                continue;
            }

            Ensure(document, section, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.FirstPage);
            Ensure(document, section, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.FirstPage);
        }
    }

    /// <summary>Sets different odd/even mode and creates odd/even header/footer targets when enabled.</summary>
    public static void SetDifferentOddAndEvenPages(DocumentEditorDocument document, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsurePrimaryHeadersFooters(document);

        foreach (var section in document.Sections)
        {
            section.Properties.DifferentOddAndEvenPages = enabled;
            if (!enabled)
            {
                continue;
            }

            Ensure(document, section, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.EvenPages);
            Ensure(document, section, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.EvenPages);
            Ensure(document, section, DocumentHeaderFooterType.Header, DocumentHeaderFooterScope.OddPages);
            Ensure(document, section, DocumentHeaderFooterType.Footer, DocumentHeaderFooterScope.OddPages);
        }
    }

    /// <summary>Resolves the header/footer definition used for a rendered page.</summary>
    public static DocumentHeaderFooter? Resolve(
        DocumentEditorDocument document,
        DocumentSection section,
        DocumentHeaderFooterType type,
        int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(section);

        var scope = ResolveScope(section.Properties, pageIndex);
        return ResolveByReference(document, section, type, scope)
            ?? ResolveByReference(document, section, type, DocumentHeaderFooterScope.Primary);
    }

    /// <summary>Ensures and returns a header/footer definition for a section and scope.</summary>
    public static DocumentHeaderFooter Ensure(
        DocumentEditorDocument document,
        DocumentSection section,
        DocumentHeaderFooterType type,
        DocumentHeaderFooterScope scope)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(section);

        document.HeadersFooters ??= [];
        section.Properties ??= new DocumentSectionProperties();
        section.Properties.HeaderFooterReferences ??= [];

        var existing = ResolveByReference(document, section, type, scope);
        if (existing is not null)
        {
            EnsureEditablePlaceholder(existing);
            return existing;
        }

        existing = document.HeadersFooters.FirstOrDefault(headerFooter =>
            headerFooter.Type == type
            && headerFooter.Scope == scope
            && string.Equals(headerFooter.SectionId, section.Id, StringComparison.Ordinal));

        if (existing is null)
        {
            existing = new DocumentHeaderFooter
            {
                Type = type,
                Scope = scope,
                SectionId = section.Id
            };
            EnsureEditablePlaceholder(existing);
            document.HeadersFooters.Add(existing);
        }

        section.Properties.HeaderFooterReferences.Add(new DocumentHeaderFooterReference
        {
            HeaderFooterId = existing.Id,
            Type = type,
            Scope = scope
        });

        return existing;
    }

    /// <summary>Finds a header/footer by id.</summary>
    public static DocumentHeaderFooter? FindById(DocumentEditorDocument document, string? headerFooterId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return string.IsNullOrWhiteSpace(headerFooterId)
            ? null
            : document.HeadersFooters.FirstOrDefault(headerFooter =>
                string.Equals(headerFooter.Id, headerFooterId, StringComparison.Ordinal));
    }

    private static DocumentHeaderFooterScope ResolveScope(DocumentSectionProperties? properties, int pageIndex)
    {
        properties ??= new DocumentSectionProperties();
        if (pageIndex == 0 && properties.DifferentFirstPage)
        {
            return DocumentHeaderFooterScope.FirstPage;
        }

        if (properties.DifferentOddAndEvenPages)
        {
            var pageNumber = Math.Max(0, pageIndex) + 1;
            return pageNumber % 2 == 0
                ? DocumentHeaderFooterScope.EvenPages
                : DocumentHeaderFooterScope.OddPages;
        }

        return DocumentHeaderFooterScope.Primary;
    }

    private static DocumentHeaderFooter? ResolveByReference(
        DocumentEditorDocument document,
        DocumentSection section,
        DocumentHeaderFooterType type,
        DocumentHeaderFooterScope scope)
    {
        var reference = section.Properties?.HeaderFooterReferences.FirstOrDefault(item =>
            item.Type == type && item.Scope == scope);
        if (reference is null)
        {
            return null;
        }

        return FindById(document, reference.HeaderFooterId);
    }

    private static void EnsureEditablePlaceholder(DocumentHeaderFooter headerFooter)
    {
        headerFooter.Blocks ??= [];
        if (headerFooter.Blocks.Count > 0)
        {
            return;
        }

        headerFooter.Blocks.Add(new DocumentBlock
        {
            Type = DocumentBlockType.Paragraph,
            Order = 10,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Text = string.Empty }]
            }
        });
    }
}
