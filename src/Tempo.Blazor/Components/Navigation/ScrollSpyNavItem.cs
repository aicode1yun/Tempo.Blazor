namespace Tempo.Blazor.Components.Navigation;

/// <summary>
/// A single entry in a <see cref="TmScrollSpyNav"/>. Intentionally minimal/generic — hosts that need
/// badges, status dots, or required markers should supply a custom <c>ItemTemplate</c> instead of
/// extending this record with domain-specific fields.
/// </summary>
/// <param name="Id">Stable identifier. Must match the <c>id</c> attribute of the corresponding in-page section.</param>
/// <param name="Label">Display text shown by the default item rendering.</param>
/// <param name="IsVisible">When false, the item is excluded from the rendered list.</param>
/// <param name="Order">Sort order among visible items (ascending).</param>
public sealed record ScrollSpyNavItem(string Id, string Label, bool IsVisible = true, int Order = 0);
