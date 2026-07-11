namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>What a column list should do when it finds no columns stored under it.</summary>
internal enum ColumnListAction
{
    /// <summary>The columns are there; render them.</summary>
    Keep,

    /// <summary>A brand-new column list: create its default columns once.</summary>
    Seed,

    /// <summary>It once had columns and now has none: an invisible shell that must go.</summary>
    Collapse
}

/// <summary>
/// Decides between seeding and collapsing an empty column list. Seeding unconditionally — the old
/// behaviour — resurrected the two default columns on every single load, so a column list the user
/// had emptied could never be removed.
/// </summary>
internal static class NotionColumnListSeeding
{
    public static ColumnListAction Decide(int declaredColumnCount, int storedColumnCount)
    {
        if (storedColumnCount > 0) return ColumnListAction.Keep;

        // A column list that has never been laid out declares no columns yet.
        return declaredColumnCount <= 0 ? ColumnListAction.Seed : ColumnListAction.Collapse;
    }
}
