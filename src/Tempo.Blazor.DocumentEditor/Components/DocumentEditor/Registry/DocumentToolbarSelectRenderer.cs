using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for document toolbar select inputs.</summary>
public sealed class DocumentToolbarSelectRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.Select;

    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "tm-document-editor__ribbon-select");
        builder.AddAttribute(2, "data-toolbar-item", context.Item.Id);
        builder.OpenElement(3, "span");
        builder.AddContent(4, context.Item.LabelKey ?? context.Item.Id);
        builder.CloseElement();
        builder.OpenElement(5, "select");
        builder.AddAttribute(6, "data-command", context.Item.CommandName);
        builder.CloseElement();
        builder.CloseElement();
    };
}
