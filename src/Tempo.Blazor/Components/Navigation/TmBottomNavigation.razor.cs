using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.Navigation;

/// <summary>A mobile bottom navigation bar component.</summary>
public partial class TmBottomNavigation : ComponentBase
{
    /// <summary>The navigation items.</summary>
    [Parameter] public IReadOnlyList<BottomNavItem> Items { get; set; } = [];

    /// <summary>The currently selected item.</summary>
    [Parameter] public BottomNavItem? SelectedItem { get; set; }

    /// <summary>Event fired when an item is clicked.</summary>
    [Parameter] public EventCallback<BottomNavItem> OnItemClick { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    private async Task HandleClick(BottomNavItem item)
    {
        if (item.Disabled)
            return;

        SelectedItem = item;
        await OnItemClick.InvokeAsync(item);
    }
}
