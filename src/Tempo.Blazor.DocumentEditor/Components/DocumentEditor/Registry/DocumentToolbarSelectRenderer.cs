using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Default renderer for document toolbar select inputs.</summary>
public sealed class DocumentToolbarSelectRenderer : IDocumentToolbarItemRenderer
{
    /// <inheritdoc />
    public DocumentToolbarItemKind Kind => DocumentToolbarItemKind.Select;

    /// <inheritdoc />
    public RenderFragment Render(DocumentToolbarRenderContext context) => builder =>
    {
        var state = context.CommandState;
        builder.OpenElement(0, "label");
        builder.AddAttribute(1, "class", "tm-document-editor__ribbon-select");
        builder.AddAttribute(2, "data-toolbar-item", context.Item.Id);
        builder.OpenElement(3, "span");
        builder.AddContent(4, context.Item.LabelKey ?? context.Item.Id);
        builder.CloseElement();
        builder.OpenElement(5, "select");
        builder.AddAttribute(6, "data-command", context.Item.CommandName);
        // Fáze 17: hodnota + enabled z registry stavu, změna přes command context.
        if (state?.Value is { } value)
        {
            builder.AddAttribute(7, "value", value);
        }

        if (state is { IsEnabled: false })
        {
            builder.AddAttribute(8, "disabled", true);
        }

        if (context.Execute.HasDelegate)
        {
            var execute = context.Execute;
            builder.AddAttribute(9, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this,
                args => execute.InvokeAsync(args.Value?.ToString())));
        }

        foreach (var option in context.Item.Options ?? [])
        {
            builder.OpenElement(10, "option");
            builder.SetKey(option.Value);
            builder.AddAttribute(11, "value", option.Value);
            builder.AddContent(12, option.EffectiveLabel);
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
    };
}
