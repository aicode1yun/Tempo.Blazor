namespace Tempo.Blazor.Components.Inputs;

/// <summary>Result of a <see cref="TmUserPicker{TUser}.SearchProvider"/> invocation.</summary>
/// <typeparam name="T">The picked entity type.</typeparam>
/// <param name="Items">Matching items. Empty (and typically unused) when <paramref name="State"/> is not <see cref="TmPickerFetchState.Ok"/>.</param>
/// <param name="State">Whether the search found results, found none, or failed transiently.</param>
public sealed record TmPickerSearchResult<T>(IReadOnlyList<T> Items, TmPickerFetchState State);
