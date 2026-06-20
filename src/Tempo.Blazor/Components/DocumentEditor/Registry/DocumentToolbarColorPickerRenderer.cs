using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for document toolbar color picker inputs.</summary>
public sealed class DocumentToolbarColorPickerRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.ColorPicker;

    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "tm-document-editor__ribbon-color");
        builder.AddAttribute(2, "data-toolbar-item", context.Item.Id);
        builder.OpenElement(3, "span");
        builder.AddContent(4, context.Item.LabelKey ?? context.Item.Id);
        builder.CloseElement();
        builder.OpenElement(5, "input");
        builder.AddAttribute(6, "type", "color");
        builder.AddAttribute(7, "data-command", context.Item.CommandName);
        builder.CloseElement();
        builder.CloseElement();
    };
}
