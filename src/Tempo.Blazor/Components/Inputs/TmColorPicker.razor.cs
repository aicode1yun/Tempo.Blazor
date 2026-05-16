using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Inputs;

public partial class TmColorPicker
{
    private bool _isOpen;
    private string? _pendingValue;

    /// <summary>The current color value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Fires when the color value changes.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Output color format. Defaults to hex.</summary>
    [Parameter] public ColorFormat Format { get; set; } = ColorFormat.Hex;

    /// <summary>Shows alpha channel controls.</summary>
    [Parameter] public bool ShowAlpha { get; set; } = true;

    /// <summary>Shows the preset color palette.</summary>
    [Parameter] public bool ShowPalette { get; set; } = true;

    /// <summary>Shows the clear color command.</summary>
    [Parameter] public bool ShowClearButton { get; set; } = true;

    /// <summary>
    /// When true, the dropdown shows an Apply button and only closes when the user explicitly clicks it.
    /// Changing the color inside the picker does not close the dropdown.
    /// </summary>
    [Parameter] public bool ShowApplyButton { get; set; }

    /// <summary>Shows a cancel button next to Apply when <see cref="ShowApplyButton"/> is true.</summary>
    [Parameter] public bool ShowCancelButton { get; set; }

    /// <summary>Optional apply button text.</summary>
    [Parameter] public string? ApplyText { get; set; }

    /// <summary>Optional cancel button text.</summary>
    [Parameter] public string? CancelText { get; set; }

    /// <summary>Disables the color picker.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Placeholder text shown when no color is selected.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional attributes rendered on the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private void ToggleDropdown()
    {
        if (Disabled)
        {
            return;
        }

        _isOpen = !_isOpen;
        if (_isOpen)
        {
            _pendingValue = Value;
        }
    }

    private async Task OnFlatValueChangedAsync(string? value)
    {
        if (Disabled)
        {
            return;
        }

        _pendingValue = value;
        if (!ShowApplyButton)
        {
            await ValueChanged.InvokeAsync(value);
            _isOpen = false;
        }
    }

    private async Task ApplyAsync()
    {
        if (Disabled)
        {
            return;
        }

        await ValueChanged.InvokeAsync(_pendingValue);
        _isOpen = false;
    }

    private Task CancelAsync()
    {
        _pendingValue = Value;
        _isOpen = false;
        return Task.CompletedTask;
    }
}
