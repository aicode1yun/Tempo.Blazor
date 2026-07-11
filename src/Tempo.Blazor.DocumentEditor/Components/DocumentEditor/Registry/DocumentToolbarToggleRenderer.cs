using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for document toolbar toggle buttons.</summary>
public sealed class DocumentToolbarToggleRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        var state = context.CommandState;
        // Fáze 17: aria-pressed odráží skutečný stav z registry ("active" → true, "mixed" → mixed).
        var pressed = state?.Value switch
        {
            "active" => "true",
            "mixed" => "mixed",
            _ => "false"
        };

        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", pressed == "false"
            ? "tm-document-editor__ribbon-button"
            : "tm-document-editor__ribbon-button tm-document-editor__ribbon-button--active");
        builder.AddAttribute(3, "aria-pressed", pressed);
        builder.AddAttribute(4, "data-command", context.Item.CommandName);
        builder.AddAttribute(5, "data-toolbar-item", context.Item.Id);
        if (state is { IsEnabled: false })
        {
            builder.AddAttribute(6, "disabled", true);
        }

        if (context.Execute.HasDelegate)
        {
            var execute = context.Execute;
            builder.AddAttribute(7, "onclick", EventCallback.Factory.Create<MouseEventArgs>(
                this,
                _ => execute.InvokeAsync(null)));
        }

        builder.AddContent(8, context.Item.LabelKey ?? context.Item.CommandName ?? context.Item.Id);
        builder.CloseElement();
    };

    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.Toggle;
}
