namespace Tempo.Blazor.Components.DocumentEditor.Registry;

public sealed record DocumentToolbarItem
{
    /// <summary>Stable toolbar item identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Optional command registry name executed by this toolbar item.</summary>
    public string? CommandName { get; init; }

    /// <summary>Optional icon name.</summary>
    public string? Icon { get; init; }

    /// <summary>Localization key for the item label.</summary>
    public string? LabelKey { get; init; }

    /// <summary>Renderer kind for this item.</summary>
    public DocumentToolbarItemKind Kind { get; init; } = DocumentToolbarItemKind.Button;

    /// <summary>Logical tab that owns this item.</summary>
    public DocumentToolbarTab Tab { get; init; } = DocumentToolbarTab.Home;

    /// <summary>Logical toolbar group. Prefer this over <see cref="GroupId"/> for new code.</summary>
    public string? Group { get; init; }

    /// <summary>Backward-compatible toolbar group identifier.</summary>
    public string? GroupId { get; init; }

    /// <summary>Sort order inside the tab/group.</summary>
    public int Order { get; init; }

    /// <summary>Overflow priority for this item.</summary>
    public ToolbarItemPriority Priority { get; init; } = ToolbarItemPriority.Primary;

    /// <summary>Optional runtime visibility predicate.</summary>
    public Func<DocumentToolbarVisibilityContext, bool>? VisibleWhen { get; init; }

    /// <summary>Options rendered by the declarative select renderer (Fáze 17). Null for non-select items.</summary>
    public IReadOnlyList<DocumentToolbarItemOption>? Options { get; init; }

    /// <summary>Resolved group identifier.</summary>
    public string? EffectiveGroup => Group ?? GroupId;

    /// <summary>Returns whether the item is visible in the given runtime context.</summary>
    public bool IsVisible(DocumentToolbarVisibilityContext context) =>
        VisibleWhen?.Invoke(context) ?? true;

    /// <summary>Sorts items by tab, group, and order while preserving equal-key order.</summary>
    public static IEnumerable<DocumentToolbarItem> SortByOrder(IEnumerable<DocumentToolbarItem> items) =>
        items
            .OrderBy(i => i.Tab)
            .ThenBy(i => i.EffectiveGroup ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(i => i.Order);

    /// <summary>Sorts items by overflow priority, tab, group, and order.</summary>
    public static IEnumerable<DocumentToolbarItem> SortForOverflow(IEnumerable<DocumentToolbarItem> items) =>
        items
            .OrderBy(i => i.Priority)
            .ThenBy(i => i.Tab)
            .ThenBy(i => i.EffectiveGroup ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(i => i.Order);
}
