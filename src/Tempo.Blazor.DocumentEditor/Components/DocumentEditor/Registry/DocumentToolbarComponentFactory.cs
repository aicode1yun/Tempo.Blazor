using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Factory that resolves renderers for declarative document toolbar items.</summary>
public sealed class DocumentToolbarComponentFactory
{
    private readonly Dictionary<DocumentToolbarItemKind, IDocumentToolbarItemRenderer> _renderers = [];

    /// <summary>Creates a factory with the built-in document toolbar renderers registered.</summary>
    public static DocumentToolbarComponentFactory CreateDefault()
    {
        var factory = new DocumentToolbarComponentFactory();
        factory.Register(new DocumentToolbarButtonRenderer());
        factory.Register(new DocumentToolbarToggleRenderer());
        factory.Register(new DocumentToolbarSelectRenderer());
        factory.Register(new DocumentToolbarColorPickerRenderer());
        factory.Register(new DocumentToolbarSplitButtonRenderer());
        factory.Register(new DocumentToolbarMenuRenderer());
        factory.Register(new DocumentToolbarGridPickerRenderer());
        factory.Register(new DocumentToolbarSeparatorRenderer());
        return factory;
    }

    /// <summary>Registers or replaces a renderer for its declared toolbar item kind.</summary>
    public void Register(IDocumentToolbarItemRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers[renderer.Kind] = renderer;
    }

    /// <summary>Gets the renderer registered for the requested toolbar item kind.</summary>
    public IDocumentToolbarItemRenderer GetRenderer(DocumentToolbarItemKind kind)
    {
        if (_renderers.TryGetValue(kind, out var renderer))
        {
            return renderer;
        }

        throw new InvalidOperationException(
            $"No document toolbar renderer is registered for kind '{kind}'. Register a renderer before rendering this toolbar item.");
    }

    /// <summary>Renders a toolbar item using the renderer registered for its kind.</summary>
    public RenderFragment Render(DocumentToolbarRenderContext context) =>
        GetRenderer(context.Item.Kind).Render(context);
}
