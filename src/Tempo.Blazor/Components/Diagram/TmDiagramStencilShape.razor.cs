using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Diagram.Models;
using Tempo.Blazor.Components.Diagram.Stencils;

namespace Tempo.Blazor.Components.Diagram;

/// <summary>Renders a diagram stencil shape inside a node.</summary>
public partial class TmDiagramStencilShape : ComponentBase
{
    [Parameter] public DiagramNode Node { get; set; } = default!;
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public EventCallback<string> OnPortMouseDownEvent { get; set; }
    [Parameter] public EventCallback<(string DataKey, object Value)> OnSectionEdit { get; set; }

    [Inject] private DiagramStencilRegistry StencilRegistry { get; set; } = default!;

    private DiagramStencil? _stencil;
    private string? _editingDataKey;
    private string _editingText = "";
    private bool _editFocusPending;
    private ElementReference _editInputRef;
    private ElementReference _editTextareaRef;

    protected override void OnParametersSet()
    {
        _stencil = StencilRegistry.GetStencil(Node.StencilId);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_editFocusPending && _editingDataKey is not null)
        {
            _editFocusPending = false;
            var section = _stencil?.Layout.Sections.FirstOrDefault(s => s.DataKey == _editingDataKey);
            try
            {
                if (section?.Type == "list")
                    await _editTextareaRef.FocusAsync();
                else
                    await _editInputRef.FocusAsync();
            }
            catch { }
        }
    }

    private void StartEdit(string? dataKey, string text)
    {
        if (string.IsNullOrEmpty(dataKey)) return;
        _editingDataKey = dataKey;
        _editingText = text;
        _editFocusPending = true;
    }

    private void StartEditList(string? dataKey, IEnumerable<string> list)
    {
        if (string.IsNullOrEmpty(dataKey)) return;
        _editingDataKey = dataKey;
        _editingText = string.Join("\n", list);
        _editFocusPending = true;
    }

    private void SaveEdit()
    {
        if (string.IsNullOrEmpty(_editingDataKey)) return;
        var section = _stencil?.Layout.Sections.FirstOrDefault(s => s.DataKey == _editingDataKey);
        object value = section?.Type == "list"
            ? _editingText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim())
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .ToList()
            : _editingText;
        _ = OnSectionEdit.InvokeAsync((_editingDataKey, value));
        _editingDataKey = null;
        _editingText = "";
    }

    private void CancelEdit()
    {
        _editingDataKey = null;
        _editingText = "";
    }

    private void OnEditKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            SaveEdit();
        }
        else if (e.Key == "Escape")
        {
            CancelEdit();
        }
    }

    private void OnEditFocusOut(FocusEventArgs e)
    {
        if (_editingDataKey is not null)
        {
            SaveEdit();
        }
    }

    private string GetShapeStyle()
    {
        var layout = _stencil?.Layout;
        var fill = layout?.Fill ?? Node.Style.Fill ?? "#ffffff";
        var stroke = layout?.Stroke ?? Node.Style.Stroke ?? "#111827";
        var strokeWidth = layout?.StrokeWidth ?? Node.Style.StrokeWidth ?? 1.5;
        var radius = GetBorderRadius();
        var shape = layout?.BackgroundShape ?? "rectangle";

        var borderStyle = shape == "weak-entity" ? "double" : "solid";
        var hasClipPath = shape is "diamond" or "document";

        if (hasClipPath)
        {
            var style = $"background: {stroke}; padding: {F(strokeWidth)}px; width: 100%; height: 100%; overflow: hidden; box-sizing: border-box;";
            if (shape == "diamond")
                style += " clip-path: polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%);";
            else if (shape == "document")
                style += " clip-path: polygon(0% 0%, 100% 0%, 100% 80%, 85% 100%, 70% 80%, 55% 100%, 40% 80%, 25% 100%, 10% 80%, 0% 100%);";
            return style;
        }

        return $"background: {fill}; border: {F(strokeWidth)}px {borderStyle} {stroke}; border-radius: {radius}; width: 100%; height: 100%; overflow: hidden;";
    }

    private string GetInnerWrapperStyle()
    {
        var layout = _stencil?.Layout;
        var fill = layout?.Fill ?? Node.Style.Fill ?? "#ffffff";
        var shape = layout?.BackgroundShape ?? "rectangle";
        var hasClipPath = shape is "diamond" or "document";

        if (!hasClipPath)
            return "width: 100%; height: 100%;";

        var style = $"background: {fill}; width: 100%; height: 100%; overflow: hidden;";
        if (shape == "diamond")
            style += " clip-path: polygon(50% 0%, 100% 50%, 50% 100%, 0% 50%);";
        else if (shape == "document")
            style += " clip-path: polygon(0% 0%, 100% 0%, 100% 80%, 85% 100%, 70% 80%, 55% 100%, 40% 80%, 25% 100%, 10% 80%, 0% 100%);";
        return style;
    }

    private string GetContentStyle()
    {
        if (_stencil?.Layout.BackgroundShape == "diamond")
        {
            return "transform: rotate(0deg);"; // Content stays upright; diamond is shape-level
        }
        if (_stencil?.Layout.BackgroundShape == "document")
        {
            return "padding-bottom: 8px;"; // Leave room for scalloped bottom
        }
        return "";
    }

    private string GetBorderRadius()
    {
        return _stencil?.Layout.BackgroundShape switch
        {
            "rounded" => "8px",
            "ellipse" => "9999px",
            _ => "0px",
        };
    }

    private static string GetTextStyle(DiagramStencilTextStyle? ts)
    {
        if (ts is null) return "";
        var sb = new System.Text.StringBuilder();
        sb.Append($"text-align: {GetTextAlign(ts.TextAlign)};");
        if (ts.Color is not null) sb.Append($" color: {ts.Color};");
        if (ts.FontSize is not null) sb.Append($" font-size: {F(ts.FontSize.Value)}px;");
        if (ts.FontFamily is not null) sb.Append($" font-family: {ts.FontFamily};");
        if (ts.TextTransform is not null) sb.Append($" text-transform: {ts.TextTransform};");
        if (ts.LetterSpacing is not null) sb.Append($" letter-spacing: {ts.LetterSpacing};");
        return sb.ToString();
    }

    private static string GetTextInnerStyle(DiagramStencilTextStyle? ts)
    {
        if (ts is null) return "";
        var sb = new System.Text.StringBuilder();
        if (ts.IsBold) sb.Append(" font-weight: 700;");
        if (ts.IsItalic) sb.Append(" font-style: italic;");
        return sb.ToString();
    }

    private static string GetTextAlign(StencilTextAlign align)
        => align switch
        {
            StencilTextAlign.Center => "center",
            StencilTextAlign.Right => "right",
            _ => "left",
        };

    private string GetSectionText(DiagramStencilSection section)
    {
        if (section.DataKey is not null && Node.Data.TryGetValue(section.DataKey, out var value))
        {
            var text = value?.ToString();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return section.DefaultText ?? "";
    }

    private IEnumerable<string> GetSectionList(DiagramStencilSection section)
    {
        if (section.DataKey is not null && Node.Data.TryGetValue(section.DataKey, out var value))
        {
            if (value is System.Text.Json.JsonElement je)
            {
                if (je.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    return je.EnumerateArray().Select(e => e.ToString()).ToList();
                }
                return [je.ToString()];
            }
            if (value is IEnumerable<string> strs)
            {
                return strs;
            }
            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                return enumerable.Cast<object>().Select(o => o?.ToString() ?? "").ToList();
            }
            var s = value?.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return [s];
        }
        var def = section.DefaultText;
        if (!string.IsNullOrWhiteSpace(def)) return [def];
        return [];
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
