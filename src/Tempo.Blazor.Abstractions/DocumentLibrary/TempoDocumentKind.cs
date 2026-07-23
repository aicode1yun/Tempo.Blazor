namespace Tempo.Blazor.DocumentLibrary;

/// <summary>
/// Identifies which editor produced a stored document, so that a generic document
/// library (browse/open dialog, MCP tooling) can route to the correct payload type
/// and provider without knowing the concrete document model.
/// </summary>
/// <remarks>
/// Serialised as a camelCase string (e.g. <c>"wireframe"</c>) for AI/MCP readability.
/// </remarks>
public enum TempoDocumentKind
{
    /// <summary>A wireframe document produced by the wireframe editor.</summary>
    Wireframe,

    /// <summary>A diagram document produced by the diagram editor.</summary>
    Diagram,

    /// <summary>A spreadsheet workbook produced by the spreadsheet editor.</summary>
    Spreadsheet,

    /// <summary>An architecture/modeling model produced by the modeling editor.</summary>
    Modeling
}
