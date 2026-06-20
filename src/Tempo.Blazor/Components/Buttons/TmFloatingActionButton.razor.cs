using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.Buttons;

/// <summary>A floating action button component with optional speed dial menu.</summary>
public partial class TmFloatingActionButton : ComponentBase
{
    private bool _isOpen;

    /// <summary>The icon displayed on the main button. Defaults to a plus icon.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>The position of the button on the screen. Defaults to BottomRight.</summary>
    [Parameter] public FabPosition Position { get; set; } = FabPosition.BottomRight;

    /// <summary>Whether the button is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Optional speed dial items. When provided, clicking the main button opens a menu.</summary>
    [Parameter] public IReadOnlyList<FabItem>? Items { get; set; }

    /// <summary>Event fired when the main button is clicked.</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>Event fired when a speed dial item is clicked.</summary>
    [Parameter] public EventCallback<FabItem> OnItemClick { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    private async Task HandleMainClick()
    {
        if (Disabled)
            return;

        if (Items?.Count > 0)
        {
            _isOpen = !_isOpen;
        }
        else
        {
            await OnClick.InvokeAsync();
        }
    }

    private async Task HandleItemClick(FabItem item)
    {
        if (item.Disabled)
            return;

        _isOpen = false;
        await OnItemClick.InvokeAsync(item);
    }
}
