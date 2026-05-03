using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>A range slider component for selecting a numeric range with two handles.</summary>
public partial class TmRangeSlider : ComponentBase
{
    /// <summary>The minimum value of the slider. Defaults to 0.</summary>
    [Parameter] public int Min { get; set; }

    /// <summary>The maximum value of the slider. Defaults to 100.</summary>
    [Parameter] public int Max { get; set; } = 100;

    /// <summary>The step increment. Defaults to 1.</summary>
    [Parameter] public int Step { get; set; } = 1;

    /// <summary>The start value of the range.</summary>
    [Parameter] public int? StartValue { get; set; }

    /// <summary>Event fired when the start value changes.</summary>
    [Parameter] public EventCallback<int?> StartValueChanged { get; set; }

    /// <summary>The end value of the range.</summary>
    [Parameter] public int? EndValue { get; set; }

    /// <summary>Event fired when the end value changes.</summary>
    [Parameter] public EventCallback<int?> EndValueChanged { get; set; }

    /// <summary>Whether to display the current values.</summary>
    [Parameter] public bool ShowValue { get; set; }

    /// <summary>Label shown above the slider.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Whether the slider is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Additional CSS classes.</summary>
    [Parameter] public string? AdditionalCssClass { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private int _currentStartValue;
    private int _currentEndValue;
    private ElementReference _containerRef;
    private bool _jsInitialized;

    protected override void OnParametersSet()
    {
        _currentStartValue = StartValue.GetValueOrDefault(Min);
        _currentEndValue = EndValue.GetValueOrDefault(Max);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !Disabled)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("eval", @"
                    if (!window.TempoSlider) {
                        window.TempoSlider = {
                            initRangeSlider: function(container) {
                                const startInput = container.querySelector('.tm-range-slider__input--start');
                                const endInput = container.querySelector('.tm-range-slider__input--end');                                
                                if (!startInput || !endInput) {
                                    console.error('[TmRangeSlider] inputs not found');
                                    return;
                                }
                                
                                let activeInput = null;
                                let min = parseFloat(startInput.min);
                                let max = parseFloat(startInput.max);
                                let step = parseFloat(startInput.step) || 1;
                                
                                function getValueFromX(clientX) {
                                    const rect = container.getBoundingClientRect();
                                    const x = (clientX - rect.left) / rect.width;
                                    const pct = Math.max(0, Math.min(1, x));
                                    let val = min + pct * (max - min);
                                    val = Math.round(val / step) * step;
                                    return Math.max(min, Math.min(val, max));
                                }
                                
                                function updateValue(input, value) {
                                    if (parseFloat(input.value) !== value) {
                                        input.value = value;
                                        input.dispatchEvent(new Event('input', { bubbles: true }));
                                    }
                                }
                                
                                function onPointerDown(e) {
                                    if (e.button !== 0) return;
                                    const rect = container.getBoundingClientRect();
                                    const x = (e.clientX - rect.left) / rect.width;
                                    const startVal = parseFloat(startInput.value);
                                    const endVal = parseFloat(endInput.value);
                                    const startPos = (startVal - min) / (max - min);
                                    const endPos = (endVal - min) / (max - min);
                                    const startCloser = Math.abs(x - startPos) < Math.abs(x - endPos);
                                    
                                    activeInput = startCloser ? startInput : endInput;                                    
                                    
                                    e.preventDefault();
                                    
                                    const val = getValueFromX(e.clientX);
                                    if (activeInput === startInput) {
                                        updateValue(startInput, Math.min(val, parseFloat(endInput.value)));
                                    } else {
                                        updateValue(endInput, Math.max(val, parseFloat(startInput.value)));
                                    }
                                }
                                
                                function onPointerMove(e) {
                                    if (!activeInput) return;
                                    const val = getValueFromX(e.clientX);
                                    if (activeInput === startInput) {
                                        updateValue(startInput, Math.min(val, parseFloat(endInput.value)));
                                    } else {
                                        updateValue(endInput, Math.max(val, parseFloat(startInput.value)));
                                    }
                                }
                                
                                function onPointerUp() {                                    
                                    activeInput = null;
                                }
                                
                                container.addEventListener('pointerdown', onPointerDown);
                                window.addEventListener('pointermove', onPointerMove);
                                window.addEventListener('pointerup', onPointerUp);
                                window.addEventListener('pointercancel', onPointerUp);                                
                            }
                        };
                    }
                ");
                await JSRuntime.InvokeVoidAsync("TempoSlider.initRangeSlider", _containerRef);
                _jsInitialized = true;
            }
            catch
            {
                // JS interop may fail in prerendering or tests
            }
        }
    }

    private string GetFillStyle()
    {
        var range = Max - Min;

        if (range <= 0) return "left: 0%; width: 100%;";

        var left = ((double)(_currentStartValue - Min) / range * 100);
        var width = ((double)(_currentEndValue - _currentStartValue) / range * 100);

        var leftStr = left.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        var widthStr = width.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        return $"left: {leftStr}%; width: {widthStr}%;";
    }

    private async Task SetStartValueAsync(int value)
    {
        value = Math.Max(Min, Math.Min(value, _currentEndValue));
        if (_currentStartValue != value)
        {
            _currentStartValue = value;
            await StartValueChanged.InvokeAsync(value);
            StateHasChanged();
        }
    }

    private async Task SetEndValueAsync(int value)
    {
        value = Math.Max(_currentStartValue, Math.Min(value, Max));
        if (_currentEndValue != value)
        {
            _currentEndValue = value;
            await EndValueChanged.InvokeAsync(value);
            StateHasChanged();
        }
    }
}
