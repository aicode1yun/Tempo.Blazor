namespace Tempo.Blazor.DocumentEditor.Models;

/// <summary>Snapshot exchanged between the WYSIWYG JS engine and Blazor.</summary>
public sealed class WysiwygDocumentSnapshot
{
    /// <summary>Protocol version of the snapshot.</summary>
    public int ProtocolVersion { get; set; } = 1;

    /// <summary>Document payload.</summary>
    public DocumentEditorDocument Document { get; set; } = new();

    /// <summary>Pending provider-backed suggestions to decorate in the surface.</summary>
    public IReadOnlyList<DocumentSuggestion> Suggestions { get; set; } = [];
}
