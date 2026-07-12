namespace Tempo.Blazor.Components.Inputs;

/// <summary>Result of a <see cref="TmUserPicker{TUser}.ResolveProvider"/> invocation.</summary>
/// <typeparam name="T">The picked entity type.</typeparam>
/// <param name="Item">The resolved item, or <c>default</c> when not found or when <paramref name="State"/> is <see cref="TmPickerFetchState.Transient"/>.</param>
/// <param name="State">Whether the value resolved, resolved to nothing, or failed transiently.</param>
public sealed record TmPickerResolveResult<T>(T? Item, TmPickerFetchState State);
