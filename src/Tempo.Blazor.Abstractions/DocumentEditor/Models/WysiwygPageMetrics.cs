namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Runtime metrics for a single rendered or virtual document page.</summary>
public sealed class WysiwygPageMetric
{
    /// <summary>Zero-based page index.</summary>
    public int PageIndex { get; set; }

    /// <summary>One-based page number shown to users.</summary>
    public int PageNumber { get; set; }

    /// <summary>Localized or runtime-provided page label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether the page is represented by a lightweight virtual placeholder.</summary>
    public bool IsVirtual { get; set; }

    /// <summary>Whether the rendered page currently overflows its page box.</summary>
    public bool HasOverflow { get; set; }

    /// <summary>Block ids assigned to this page by the WYSIWYG runtime.</summary>
    public IReadOnlyList<string> BlockIds { get; set; } = [];
}

/// <summary>Runtime page metrics reported by the WYSIWYG editor.</summary>
public sealed class WysiwygPageMetrics
{
    /// <summary>Total number of logical pages.</summary>
    public int TotalPages { get; set; }

    /// <summary>Currently rendered non-virtual page count.</summary>
    public int RenderedPages { get; set; }

    /// <summary>Virtual placeholder page count.</summary>
    public int VirtualizedPages { get; set; }

    /// <summary>Zero-based active page index.</summary>
    public int ActivePageIndex { get; set; }

    /// <summary>Page metrics in document order.</summary>
    public IReadOnlyList<WysiwygPageMetric> Pages { get; set; } = [];
}
