using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Inputs;

public partial class TmColorPicker
{
    private bool _isOpen;
    private string? _pendingValue;

    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public ColorFormat Format { get; set; } = ColorFormat.Hex;
    [Parameter] public bool ShowAlpha { get; set; } = true;
    [Parameter] public bool ShowPalette { get; set; } = true;
    [Parameter] public bool ShowClearButton { get; set; } = true;

    /// <summary>
    /// When true, the dropdown shows an Apply button and only closes when the user explicitly clicks it.
    /// Changing the color inside the picker does not close the dropdown.
    /// </summary>
    [Parameter] public bool ShowApplyButton { get; set; }

    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private void ToggleDropdown()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
        {
            _pendingValue = Value;
        }
    }

    private async Task OnFlatValueChangedAsync(string? value)
    {
        _pendingValue = value;
        if (!ShowApplyButton)
        {
            await ValueChanged.InvokeAsync(value);
            _isOpen = false;
        }
    }

    private async Task ApplyAsync()
    {
        await ValueChanged.InvokeAsync(_pendingValue);
        _isOpen = false;
    }
}
