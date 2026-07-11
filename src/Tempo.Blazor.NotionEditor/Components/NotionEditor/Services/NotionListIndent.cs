namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Indent rules for list items. A list item may sit at most one level deeper than the item above it;
/// the first item of a list has nothing to nest under, so Tab does nothing there. Without the rule a
/// stray Tab creates a level-3 item under a level-0 one and the list renders with a hole in it.
/// </summary>
internal static class NotionListIndent
{
    /// <summary>Deepest level any list item may reach.</summary>
    public const int MaxLevel = 3;

    /// <summary>
    /// The level a list item ends up at. <paramref name="previousIndentLevel"/> is <c>null</c> when
    /// the item is the first block, or when the block above it is not a list item.
    /// </summary>
    public static int Next(int currentIndentLevel, bool outdent, int? previousIndentLevel)
    {
        if (outdent) return Math.Max(0, currentIndentLevel - 1);

        // Nothing above to nest under.
        if (previousIndentLevel is null) return currentIndentLevel;

        var deepestAllowed = Math.Min(previousIndentLevel.Value + 1, MaxLevel);

        // Indenting must never make an item shallower, even if it somehow already sits too deep.
        return Math.Max(currentIndentLevel, Math.Min(currentIndentLevel + 1, deepestAllowed));
    }
}
