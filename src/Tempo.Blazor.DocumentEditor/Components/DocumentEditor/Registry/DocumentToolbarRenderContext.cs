using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.DocumentEditor.Registry;

/// <summary>Context passed to a document toolbar item renderer.</summary>
public sealed record DocumentToolbarRenderContext(
    DocumentToolbarItem Item,
    IReadOnlyDictionary<string, object?>? Values = null,
    EventCallback<object?> Execute = default);
