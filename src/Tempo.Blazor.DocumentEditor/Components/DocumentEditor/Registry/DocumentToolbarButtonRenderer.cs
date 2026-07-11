using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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
        if (context.CommandState is { IsEnabled: false })
        {
            builder.AddAttribute(5, "disabled", true);
        }

        if (context.Execute.HasDelegate)
        {
            var execute = context.Execute;
            builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(
                this,
                _ => execute.InvokeAsync(null)));
        }

        builder.AddContent(7, context.Item.LabelKey ?? context.Item.CommandName ?? context.Item.Id);
        builder.CloseElement();
    };
}
