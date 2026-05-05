namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Display mode for the detail / properties panel in <see cref="Components.Files.TmDocumentManager{TMetadata}"/>.
/// </summary>
public enum DocumentManagerDetailMode
{
    /// <summary>Slide-in panel from the right side (like VS Code sidebar).</summary>
    SlideIn,

    /// <summary>Modal dialog overlay.</summary>
    Modal,

    /// <summary>Full page replacement / navigation.</summary>
    FullPage
}
