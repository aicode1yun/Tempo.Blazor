using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Context passed to a document toolbar item renderer.</summary>
/// <param name="Item">Toolbar item metadata being rendered.</param>
/// <param name="Values">Optional well-known state bag (SelectionToken, FormattingState, UndoState).</param>
/// <param name="Execute">Callback invoked with the command payload (selected value, color, or null for toggles).</param>
/// <param name="CommandState">Current registry state of the item's command — drives value/enabled/aria-pressed (Fáze 17; additive, default null).</param>
public sealed record DocumentToolbarRenderContext(
    DocumentToolbarItem Item,
    IReadOnlyDictionary<string, object?>? Values = null,
    EventCallback<object?> Execute = default,
    DocumentEditorCommandState? CommandState = null);
