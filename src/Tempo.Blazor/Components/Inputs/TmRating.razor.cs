using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>A rating component that displays stars and allows users to select a value.</summary>
public partial class TmRating : ComponentBase
{
    private bool _disabled;
    private int? _hoverValue;

    /// <summary>The current rating value. Null means no rating.</summary>
    [Parameter] public int? Value { get; set; }

    /// <summary>Event fired when the rating changes.</summary>
    [Parameter] public EventCallback<int?> ValueChanged { get; set; }

    /// <summary>The maximum rating value. Defaults to 5.</summary>
    [Parameter] public int Max { get; set; } = 5;

    /// <summary>Whether the rating is read-only.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Whether the rating is disabled.</summary>
    [Parameter] public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            if (value) _hoverValue = null;
        }
    }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    /// <summary>The current hover value for visual feedback.</summary>
    private int? HoverValue => _hoverValue;

    private string GetCssClass()
    {
        var classes = new System.Text.StringBuilder();
        if (Disabled) classes.Append(" tm-rating--disabled");
        if (ReadOnly) classes.Append(" tm-rating--readonly");
        if (!string.IsNullOrEmpty(AdditionalCssClass)) classes.Append(' ').Append(AdditionalCssClass);
        return classes.ToString();
    }

    private string GetStarClass(int starIndex)
    {
        var effectiveValue = HoverValue ?? Value ?? 0;
        var classes = new System.Text.StringBuilder();

        if (starIndex <= effectiveValue)
        {
            classes.Append(" tm-rating__star--full");
        }
        else
        {
            classes.Append(" tm-rating__star--empty");
        }

        if (HoverValue.HasValue && starIndex <= HoverValue.Value && (!Value.HasValue || starIndex > Value.Value))
        {
            classes.Append(" tm-rating__star--hover");
        }

        return classes.ToString();
    }

    private async Task HandleClick(int starIndex)
    {
        if (Disabled || ReadOnly)
            return;

        var newValue = Value == starIndex ? (int?)null : starIndex;
        if (Value != newValue)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(Value);
        }
    }

    private void HandleMouseOver(int starIndex)
    {
        if (Disabled || ReadOnly)
            return;

        _hoverValue = starIndex;
    }

    private void HandleMouseLeave()
    {
        _hoverValue = null;
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (Disabled || ReadOnly)
            return;

        var current = Value ?? 0;
        int? newValue = null;

        switch (e.Key)
        {
            case "ArrowRight":
            case "ArrowUp":
                if (current < Max)
                    newValue = current + 1;
                break;
            case "ArrowLeft":
            case "ArrowDown":
                if (current > 1)
                    newValue = current - 1;
                else if (current == 1)
                    newValue = null;
                break;
            case "Home":
                newValue = 1;
                break;
            case "End":
                newValue = Max;
                break;
            case "Delete":
            case "Backspace":
                newValue = null;
                break;
        }

        if (newValue != Value)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(Value);
        }
    }
}
