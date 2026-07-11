using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for document toolbar color picker inputs.</summary>
public sealed class DocumentToolbarColorPickerRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.ColorPicker;

    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        var state = context.CommandState;
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "tm-document-editor__ribbon-color");
        builder.AddAttribute(2, "data-toolbar-item", context.Item.Id);
        builder.OpenElement(3, "span");
        builder.AddContent(4, context.Item.LabelKey ?? context.Item.Id);
        builder.CloseElement();
        builder.OpenElement(5, "input");
        builder.AddAttribute(6, "type", "color");
        builder.AddAttribute(7, "data-command", context.Item.CommandName);
        // Fáze 17: hodnota + enabled z registry stavu, změna přes command context.
        if (!string.IsNullOrWhiteSpace(state?.Value))
        {
            builder.AddAttribute(8, "value", state.Value);
        }

        if (state is { IsEnabled: false })
        {
            builder.AddAttribute(9, "disabled", true);
        }

        if (context.Execute.HasDelegate)
        {
            var execute = context.Execute;
            builder.AddAttribute(10, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this,
                args => execute.InvokeAsync(args.Value?.ToString())));
        }

        builder.CloseElement();
        builder.CloseElement();
    };
}
