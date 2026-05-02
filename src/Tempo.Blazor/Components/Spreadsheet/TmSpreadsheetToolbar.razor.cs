using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// Ribbon-style toolbar for the spreadsheet with tabbed groups: Home, Insert, View, and File.
/// </summary>
public partial class TmSpreadsheetToolbar
{
    private static readonly List<SelectOption<string>> _fontOptions =
    [
        new("Arial", "Arial"),
        new("Calibri", "Calibri"),
        new("Segoe UI", "Segoe UI"),
        new("Times New Roman", "Times New Roman"),
        new("Courier New", "Courier New"),
    ];

    private static readonly List<SelectOption<string>> _fontSizeOptions =
    [
        new("8", "8"),
        new("9", "9"),
        new("10", "10"),
        new("11", "11"),
        new("12", "12"),
        new("14", "14"),
        new("16", "16"),
        new("18", "18"),
        new("20", "20"),
        new("24", "24"),
        new("28", "28"),
        new("32", "32"),
        new("36", "36"),
    ];

    private static readonly List<SelectOption<string>> _numberFormatOptions =
    [
        new("General", "General"),
        new("0", "Number"),
        new("0.00", "Number (2 dec)"),
        new("#,##0", "Number (sep)"),
        new("#,##0.00", "Number (sep, 2 dec)"),
        new("$#,##0.00", "Currency"),
        new("0%", "Percentage"),
        new("0.00%", "Percentage (2 dec)"),
        new("yyyy-MM-dd", "Date"),
        new("HH:mm", "Time"),
        new("@", "Text"),
    ];

    /// <summary>Currently active toolbar tab.</summary>
    public string ActiveTab { get; set; } = "Home";

    /// <summary>Whether undo is available.</summary>
    [Parameter] public bool CanUndo { get; set; }

    /// <summary>Whether redo is available.</summary>
    [Parameter] public bool CanRedo { get; set; }

    /// <summary>Currently selected font family.</summary>
    [Parameter] public string? SelectedFontFamily { get; set; } = "Arial";

    /// <summary>Currently selected font size.</summary>
    [Parameter] public string? SelectedFontSize { get; set; } = "11";

    /// <summary>Whether the current selection is bold.</summary>
    [Parameter] public bool IsBold { get; set; }

    /// <summary>Whether the current selection is italic.</summary>
    [Parameter] public bool IsItalic { get; set; }

    /// <summary>Whether the current selection is underlined.</summary>
    [Parameter] public bool IsUnderline { get; set; }

    /// <summary>Current text color (CSS color value).</summary>
    [Parameter] public string? TextColor { get; set; }

    /// <summary>Current background color (CSS color value).</summary>
    [Parameter] public string? BackgroundColor { get; set; }

    /// <summary>Current horizontal alignment.</summary>
    [Parameter] public string? SelectedHorizontalAlign { get; set; } = "left";

    /// <summary>Currently selected number format.</summary>
    [Parameter] public string? SelectedNumberFormat { get; set; } = "General";

    /// <summary>Whether the current selection covers a merged cell range.</summary>
    [Parameter] public bool IsMergeCellsActive { get; set; }

    /// <summary>Whether grid lines are currently visible.</summary>
    [Parameter] public bool ShowGridLines { get; set; } = true;

    /// <summary>Custom tools to inject into the toolbar.</summary>
    [Parameter] public List<SpreadsheetCustomTool>? CustomTools { get; set; }

    /// <summary>Called when the Undo button is clicked.</summary>
    [Parameter] public EventCallback OnUndo { get; set; }

    /// <summary>Called when the Redo button is clicked.</summary>
    [Parameter] public EventCallback OnRedo { get; set; }

    /// <summary>Called when the Copy button is clicked.</summary>
    [Parameter] public EventCallback OnCopy { get; set; }

    /// <summary>Called when the Cut button is clicked.</summary>
    [Parameter] public EventCallback OnCut { get; set; }

    /// <summary>Called when the Paste button is clicked.</summary>
    [Parameter] public EventCallback OnPaste { get; set; }

    /// <summary>Called when the Insert Row button is clicked.</summary>
    [Parameter] public EventCallback OnInsertRow { get; set; }

    /// <summary>Called when the Delete Row button is clicked.</summary>
    [Parameter] public EventCallback OnDeleteRow { get; set; }

    /// <summary>Called when the Insert Column button is clicked.</summary>
    [Parameter] public EventCallback OnInsertColumn { get; set; }

    /// <summary>Called when the Delete Column button is clicked.</summary>
    [Parameter] public EventCallback OnDeleteColumn { get; set; }

    /// <summary>Called when the font family changes.</summary>
    [Parameter] public EventCallback<string?> OnFontFamilyChanged { get; set; }

    /// <summary>Called when the font size changes.</summary>
    [Parameter] public EventCallback<string?> OnFontSizeChanged { get; set; }

    /// <summary>Called when the Bold toggle is clicked.</summary>
    [Parameter] public EventCallback OnBoldToggle { get; set; }

    /// <summary>Called when the Italic toggle is clicked.</summary>
    [Parameter] public EventCallback OnItalicToggle { get; set; }

    /// <summary>Called when the Underline toggle is clicked.</summary>
    [Parameter] public EventCallback OnUnderlineToggle { get; set; }

    /// <summary>Called when the text color button is clicked.</summary>
    [Parameter] public EventCallback OnTextColorClick { get; set; }

    /// <summary>Called when the background color button is clicked.</summary>
    [Parameter] public EventCallback OnBackgroundColorClick { get; set; }

    /// <summary>Called when the horizontal alignment changes.</summary>
    [Parameter] public EventCallback<string?> OnAlignChanged { get; set; }

    /// <summary>Called when the number format changes.</summary>
    [Parameter] public EventCallback<string?> OnNumberFormatChanged { get; set; }

    /// <summary>Called when the Increase Decimals button is clicked.</summary>
    [Parameter] public EventCallback OnIncreaseDecimals { get; set; }

    /// <summary>Called when the Decrease Decimals button is clicked.</summary>
    [Parameter] public EventCallback OnDecreaseDecimals { get; set; }

    /// <summary>Called when the Open button is clicked.</summary>
    [Parameter] public EventCallback OnOpen { get; set; }

    /// <summary>Called when the Download button is clicked.</summary>
    [Parameter] public EventCallback OnDownload { get; set; }

    /// <summary>Called when the Insert Link button is clicked.</summary>
    [Parameter] public EventCallback OnInsertLink { get; set; }

    /// <summary>Called when the Insert Image button is clicked.</summary>
    [Parameter] public EventCallback OnInsertImage { get; set; }

    /// <summary>Called when the Merge Cells button is clicked.</summary>
    [Parameter] public EventCallback OnMergeCells { get; set; }

    /// <summary>Called when the Toggle Grid Lines button is clicked.</summary>
    [Parameter] public EventCallback OnToggleGridLines { get; set; }

    /// <summary>Called when a custom tool is clicked.</summary>
    [Parameter] public EventCallback<SpreadsheetCustomTool> OnCustomToolClick { get; set; }

    private bool HasTextColor => !string.IsNullOrEmpty(TextColor);
    private bool HasBackgroundColor => !string.IsNullOrEmpty(BackgroundColor);

    private async Task AlignLeft() => await OnAlignChanged.InvokeAsync("left");
    private async Task AlignCenter() => await OnAlignChanged.InvokeAsync("center");
    private async Task AlignRight() => await OnAlignChanged.InvokeAsync("right");

    private static string GetActiveClass(bool isActive)
    {
        return isActive ? "tm-spreadsheet-toolbar__button--active" : string.Empty;
    }
}
