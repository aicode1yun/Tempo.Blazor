namespace Tempo.Blazor.Components.Files;

/// <summary>How a document picked in <see cref="TmDocumentOpenDialog"/> should be consumed.</summary>
public enum DocumentOpenMode
{
    /// <summary>
    /// Reference the original document. The consumer stays in sync with later edits to it.
    /// </summary>
    Link,

    /// <summary>
    /// Insert an independent copy. The consumer is unaffected by later edits to the original.
    /// </summary>
    Copy
}
