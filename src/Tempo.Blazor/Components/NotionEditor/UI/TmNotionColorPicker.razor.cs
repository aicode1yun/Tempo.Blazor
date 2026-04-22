using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.NotionEditor.UI;

public partial class TmNotionColorPicker : ComponentBase
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter] public bool    Visible           { get; set; }
    [Parameter] public double  Top               { get; set; }
    [Parameter] public double  Left              { get; set; }

    /// <summary>Currently active text-color hex, or <c>null</c> for default.</summary>
    [Parameter] public string? SelectedTextColor { get; set; }

    /// <summary>Currently active background-color hex, or <c>null</c> for default.</summary>
    [Parameter] public string? SelectedBgColor   { get; set; }

    /// <summary>
    /// Raised when a color is chosen.
    /// Exactly one element of the tuple is non-null:
    ///   (hex, null) → text color selected;
    ///   (null, hex) → background color selected.
    ///   (null, null) → default (remove color) selected for either section.
    /// </summary>
    [Parameter] public EventCallback<(string? TextColor, string? BgColor)> OnColorSelected { get; set; }

    /// <summary>Raised when the picker is dismissed without a selection.</summary>
    [Parameter] public EventCallback OnClosed { get; set; }

    // ── Internal model ────────────────────────────────────────────────────────

    private sealed record ColorEntry(string NameKey, string? Hex);

    // ── Static data ───────────────────────────────────────────────────────────

    private static readonly ColorEntry[] _textColors =
    [
        new("TmNotionColorPicker_Default", null),
        new("TmNotionColorPicker_Gray",    "#9b9a97"),
        new("TmNotionColorPicker_Brown",   "#64473a"),
        new("TmNotionColorPicker_Orange",  "#d9730d"),
        new("TmNotionColorPicker_Yellow",  "#dfab01"),
        new("TmNotionColorPicker_Green",   "#0f7b6c"),
        new("TmNotionColorPicker_Blue",    "#0b6e99"),
        new("TmNotionColorPicker_Purple",  "#6940a5"),
        new("TmNotionColorPicker_Pink",    "#ad1a72"),
        new("TmNotionColorPicker_Red",     "#e03e3e"),
    ];

    private static readonly ColorEntry[] _bgColors =
    [
        new("TmNotionColorPicker_Default",  null),
        new("TmNotionColorPicker_GrayBg",   "#ebeced"),
        new("TmNotionColorPicker_BrownBg",  "#e9e5e3"),
        new("TmNotionColorPicker_OrangeBg", "#faebdd"),
        new("TmNotionColorPicker_YellowBg", "#fbf3db"),
        new("TmNotionColorPicker_GreenBg",  "#ddedea"),
        new("TmNotionColorPicker_BlueBg",   "#ddebf1"),
        new("TmNotionColorPicker_PurpleBg", "#eae4f2"),
        new("TmNotionColorPicker_PinkBg",   "#f4dfeb"),
        new("TmNotionColorPicker_RedBg",    "#fbe4e4"),
    ];

    // ── State ─────────────────────────────────────────────────────────────────

    private double _top;
    private double _left;
    private bool   _wasVisible;

    private ElementReference _pickerRef;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible)
        {
            _top  = Top;
            _left = Left;
        }
        _wasVisible = Visible;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Visible)
        {
            try
            {
                await JS.InvokeVoidAsync("tmNotionEditor.adjustColorPickerPosition", _pickerRef);
            }
            catch { /* SSR / test */ }
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private async Task HandleTextColorAsync(string? hex)
        => await OnColorSelected.InvokeAsync((hex, null));

    private async Task HandleBgColorAsync(string? hex)
        => await OnColorSelected.InvokeAsync((null, hex));

    private async Task HandleBackdropClickAsync()
        => await OnClosed.InvokeAsync();

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            await OnClosed.InvokeAsync();
    }
}
