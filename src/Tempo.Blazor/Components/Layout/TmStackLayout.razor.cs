using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.Layout;

/// <summary>A layout component that arranges child elements in a horizontal or vertical stack.</summary>
public partial class TmStackLayout : ComponentBase
{
    /// <summary>The orientation of the stack. Defaults to Vertical.</summary>
    [Parameter] public StackOrientation Orientation { get; set; } = StackOrientation.Vertical;

    /// <summary>The gap between items in space units (1-8). Defaults to 2.</summary>
    [Parameter] public int Spacing { get; set; } = 2;

    /// <summary>How items are aligned along the cross axis.</summary>
    [Parameter] public AlignItems AlignItems { get; set; } = AlignItems.Stretch;

    /// <summary>How items are distributed along the main axis.</summary>
    [Parameter] public JustifyContent JustifyContent { get; set; } = JustifyContent.Start;

    /// <summary>Whether items should wrap to the next line when they overflow.</summary>
    [Parameter] public bool Wrap { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    /// <summary>CSS class attribute (alternative to AdditionalCssClass).</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>The content to be rendered inside the stack.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string GetStyle()
    {
        var sb = new System.Text.StringBuilder();

        sb.Append($"gap: var(--tm-space-{Spacing}); ");
        sb.Append($"align-items: {GetAlignItemsValue()}; ");
        sb.Append($"justify-content: {GetJustifyContentValue()}; ");

        if (Wrap)
        {
            sb.Append("flex-wrap: wrap; ");
        }

        return sb.ToString();
    }

    private string GetAlignItemsValue() => AlignItems switch
    {
        AlignItems.Start => "flex-start",
        AlignItems.Center => "center",
        AlignItems.End => "flex-end",
        AlignItems.Baseline => "baseline",
        _ => "stretch"
    };

    private string GetJustifyContentValue() => JustifyContent switch
    {
        JustifyContent.Start => "flex-start",
        JustifyContent.Center => "center",
        JustifyContent.End => "flex-end",
        JustifyContent.SpaceAround => "space-around",
        JustifyContent.SpaceBetween => "space-between",
        JustifyContent.SpaceEvenly => "space-evenly",
        _ => "flex-start"
    };
}
