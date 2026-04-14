using Tempo.Blazor.Components.Diagram.Models;

namespace Tempo.Blazor.Components.Diagram.Services;

/// <summary>Provides diagram stencil definitions to the diagram stencil registry.</summary>
public interface IDiagramStencilProvider
{
    /// <summary>Priority for ordering providers. Higher values override lower ones.</summary>
    int Priority { get; }

    /// <summary>Returns all stencil sets provided by this provider.</summary>
    IEnumerable<DiagramStencilSet> GetStencilSets();
}
