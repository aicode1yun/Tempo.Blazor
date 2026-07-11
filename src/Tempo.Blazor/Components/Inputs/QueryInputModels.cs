namespace Tempo.Blazor.Components.Inputs;

/// <summary>Kind of a query suggestion — drives the icon shown in the <c>TmQueryInput</c> dropdown.</summary>
public enum QuerySuggestionKind
{
    /// <summary>A queryable field/column name.</summary>
    Field,
    /// <summary>A comparison/logical operator (e.g. <c>=</c>, <c>IN</c>).</summary>
    Operator,
    /// <summary>A concrete value for a field.</summary>
    Value,
    /// <summary>A callable function (e.g. <c>currentUser()</c>).</summary>
    Function,
    /// <summary>A language keyword (e.g. <c>AND</c>, <c>ORDER BY</c>).</summary>
    Keyword
}

/// <summary>Request passed to a <c>TmQueryInput.SuggestionsProvider</c>.</summary>
/// <param name="Text">The full current query text.</param>
/// <param name="CaretPosition">Zero-based caret index within <paramref name="Text"/> the suggestions are for.</param>
public sealed record QuerySuggestionRequest(string Text, int CaretPosition);

/// <summary>A single autocomplete suggestion for <c>TmQueryInput</c>.</summary>
/// <param name="Text">Display label shown in the dropdown.</param>
/// <param name="InsertText">Text inserted at the caret (replacing the current partial token) when accepted.</param>
/// <param name="Kind">Suggestion kind — selects the icon shown beside the label.</param>
/// <param name="Description">Optional secondary text shown to the right of the label.</param>
public sealed record QuerySuggestion(
    string Text,
    string InsertText,
    QuerySuggestionKind Kind = QuerySuggestionKind.Keyword,
    string? Description = null);

/// <summary>An error range within the query text — rendered as a wavy underline with a tooltip.</summary>
/// <param name="Start">Zero-based start index of the error within the query text.</param>
/// <param name="Length">Length of the error range in characters.</param>
/// <param name="Message">Human-readable message shown as the underline's tooltip.</param>
public sealed record QueryErrorSpan(int Start, int Length, string Message);
