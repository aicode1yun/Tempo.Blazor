namespace Tempo.Blazor.Components.Inputs;

/// <summary>
/// Outcome of a <see cref="TmUserPicker{TUser}"/> search or resolve fetch. Callers must distinguish
/// <see cref="Empty"/> (the fetch succeeded and found nothing) from <see cref="Transient"/> (the fetch
/// itself failed, e.g. a network blip) — conflating the two hides real outages behind a silent
/// "no results" message.
/// </summary>
public enum TmPickerFetchState
{
    /// <summary>The fetch succeeded.</summary>
    Ok,

    /// <summary>The fetch succeeded but found no matching items.</summary>
    Empty,

    /// <summary>The fetch failed for a reason that may resolve on retry (timeout, network error, 5xx, …).</summary>
    Transient
}
