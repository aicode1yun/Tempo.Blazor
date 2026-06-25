using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Renders a declarative document toolbar item.</summary>
public interface IDocumentToolbarItemRenderer
{
    DocumentToolbarItemKind Kind { get; }

    RenderFragment Render(DocumentToolbarRenderContext context);
}
