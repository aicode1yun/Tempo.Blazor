using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for simple document toolbar buttons.</summary>
public sealed class DocumentToolbarButtonRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.Button;

    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", "tm-document-editor__ribbon-button");
        builder.AddAttribute(3, "data-command", context.Item.CommandName);
        builder.AddAttribute(4, "data-toolbar-item", context.Item.Id);
        builder.AddContent(5, context.Item.LabelKey ?? context.Item.CommandName ?? context.Item.Id);
        builder.CloseElement();
    };
}
