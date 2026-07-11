namespace Tempo.Blazor.Components.DataDisplay;

/// <summary>Definition of a Kanban board column.</summary>
public sealed record KanbanColumn(string Id, string Title, string? Color = null, int? MaxItems = null);

/// <summary>Definition of a Kanban board swimlane (a horizontal grouping of cards that spans every column).</summary>
public sealed record KanbanSwimlane(string Id, string Title, string? Color = null);

/// <summary>Event fired when a card is moved between columns or reordered within a column.</summary>
/// <typeparam name="TItem">Card item type.</typeparam>
/// <param name="Item">The dragged item.</param>
/// <param name="FromColumn">Id of the column the card was dragged from.</param>
/// <param name="ToColumn">
/// Id of the column the card was dropped on. Equal to <paramref name="FromColumn"/> for an in-column reorder.
/// </param>
/// <param name="TargetIndex">
/// Zero-based insertion index within <paramref name="ToColumn"/>'s current item ordering (as produced by the
/// board's <c>ColumnSelector</c>). The dragged item should end up positioned immediately before the item that
/// currently occupies this index; a value equal to the column's item count means "append to the end".
/// <see langword="null"/> when the event was constructed without positional data (legacy 3-argument signature).
/// </param>
/// <param name="TargetBeforeItem">
/// The item currently occupying <paramref name="TargetIndex"/> in <paramref name="ToColumn"/> — i.e. the item the
/// dragged card should be inserted before. <see langword="default"/> (typically <see langword="null"/>) when the
/// drop position is the end of the column. Prefer this over <paramref name="TargetIndex"/> when persisting order,
/// because it is unaffected by whether the dragged item is removed before or after the insertion point is computed.
/// </param>
/// <param name="FromSwimlane">
/// Id of the swimlane the card was dragged from when the board groups cards into swimlanes.
/// <see langword="null"/> when swimlanes are disabled, or when the source lane is the "no value" lane.
/// </param>
/// <param name="ToSwimlane">
/// Id of the swimlane the card was dropped on when the board groups cards into swimlanes.
/// <see langword="null"/> when swimlanes are disabled, or when the target lane is the "no value" lane
/// (i.e. the card's swimlane value should be cleared). Equal to <paramref name="FromSwimlane"/> for an in-lane reorder.
/// </param>
public sealed record KanbanMoveEvent<TItem>(
    TItem Item,
    string FromColumn,
    string ToColumn,
    int? TargetIndex = null,
    TItem? TargetBeforeItem = default,
    string? FromSwimlane = null,
    string? ToSwimlane = null);
