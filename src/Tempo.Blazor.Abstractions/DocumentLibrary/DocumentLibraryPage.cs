namespace Tempo.Blazor.DocumentLibrary;

/// <summary>One page of browse/search results plus the total count for pagination.</summary>
public sealed class DocumentLibraryPage
{
    /// <summary>The entries on this page, already sorted per the request.</summary>
    public IReadOnlyList<DocumentLibraryEntry> Items { get; set; } = [];

    /// <summary>Total number of entries matching the query across all pages.</summary>
    public int TotalCount { get; set; }
}
