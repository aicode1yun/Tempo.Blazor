using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for document toolbar separators.</summary>
public sealed class DocumentToolbarSeparatorRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.Separator;

    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "role", "separator");
        builder.AddAttribute(2, "class", "tm-document-editor__ribbon-separator");
        builder.AddAttribute(3, "data-toolbar-item", context.Item.Id);
        builder.CloseElement();
    };
}
