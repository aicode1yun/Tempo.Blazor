namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// A node in the library's virtual folder tree, used to render the dialog's left-hand
/// navigation. Flat stores return a single root with no children.
/// </summary>
public sealed class DocumentLibraryFolder
{
    /// <summary>
    /// Full path of the folder, used as its identity (e.g. <c>"/"</c> for root,
    /// <c>"/Designs/Mobile"</c> for a nested folder).
    /// </summary>
    public required string Path { get; set; }

    /// <summary>Display name of the folder (last path segment, or a label for the root).</summary>
    public required string Name { get; set; }

    /// <summary>Child folders nested directly under this one.</summary>
    public List<DocumentLibraryFolder> Children { get; set; } = [];
}
