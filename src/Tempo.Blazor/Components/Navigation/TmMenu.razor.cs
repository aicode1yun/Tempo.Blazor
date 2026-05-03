using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.Navigation;

/// <summary>A menu component that supports horizontal and vertical orientations.</summary>
public partial class TmMenu : ComponentBase
{
    /// <summary>The menu items.</summary>
    [Parameter] public IReadOnlyList<MenuItem> Items { get; set; } = [];

    /// <summary>The orientation of the menu. Defaults to Horizontal.</summary>
    [Parameter] public MenuOrientation Orientation { get; set; } = MenuOrientation.Horizontal;

    /// <summary>Event fired when an item is clicked.</summary>
    [Parameter] public EventCallback<MenuItem> OnItemClick { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    private async Task HandleClick(MenuItem item)
    {
        if (item.Disabled)
            return;

        await OnItemClick.InvokeAsync(item);
    }
}
