namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Tabs available in the document editor side panel.</summary>
public enum DocumentSidePanelTab
{
    /// <summary>Comment threads anchored in the document.</summary>
    Comments,

    /// <summary>Tracked change revisions waiting for review.</summary>
    Revisions,

    /// <summary>Document version history and version comparison.</summary>
    Versions,

    /// <summary>Document and selection properties.</summary>
    Properties,

    /// <summary>Document page thumbnails and page navigation.</summary>
    Pages,

    /// <summary>Document heading outline for navigation.</summary>
    Outline
}
