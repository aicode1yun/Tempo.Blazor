namespace Tempo.Blazor.DocumentLibrary;

/// <summary>The nature of a <see cref="TempoDocumentChange"/>.</summary>
public enum TempoDocumentChangeType
{
    /// <summary>The document's content was saved/updated.</summary>
    Saved,

    /// <summary>The document was renamed.</summary>
    Renamed,

    /// <summary>The document was deleted; subscribers should degrade references gracefully.</summary>
    Deleted
}
