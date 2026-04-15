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

    private string GetShapeStyle(bool contentBelow)
    {
        var flex = contentBelow ? "display:flex;flex-direction:column;" : "";
        return $"position:relative;width:100%;height:100%;overflow:hidden;{flex}{GetShapeStyleVars()}";
    }

    private string GetShapeStyleVars()
    {
        var layout = _stencil?.Layout;
        var fill = Node.Style.Fill ?? layout?.Fill ?? "#ffffff";
        var stroke = Node.Style.Stroke ?? layout?.Stroke ?? "#111827";
        var strokeWidth = Node.Style.StrokeWidth ?? layout?.StrokeWidth ?? 1.5;
        var opacity = Node.Style.Opacity ?? 1.0;
        var shadow = (Node.Style.HasShadow ?? false) ? "box-shadow: 2px 2px 6px rgba(0,0,0,0.25);" : "";
        var baseOpacity = $"opacity: {F(opacity)};";
        return $"--stencil-fill:{fill};--stencil-stroke:{stroke};--stencil-stroke-width:{F(strokeWidth)}px;{baseOpacity} {shadow}";
    }

    private string GetSvgBgStyle(bool contentBelow)
        => contentBelow
            ? "position:relative;flex:1 1 auto;width:100%;height:auto;overflow:hidden;"
            : "position:absolute;inset:0;width:100%;height:100%;overflow:hidden;pointer-events:none;";

    private string GetContentStyle(bool contentBelow)
    {
        var sb = new System.Text.StringBuilder();
        if (contentBelow)
        {
            sb.Append("position:relative;flex:0 0 auto;width:100%;height:auto;");
        }
        else
        {
            sb.Append("position:absolute;inset:0;");
            sb.Append("height:100%;");
        }
        sb.Append($"display:flex;justify-content:{GetVerticalAlignCss(Node.Style.VerticalAlign ?? "middle")};");
        return sb.ToString();
    }

    private string GetTextStyle(DiagramStencilTextStyle? ts)
    {
        var sb = new System.Text.StringBuilder();
        var align = Node.Style.TextAlign ?? GetTextAlign(ts?.TextAlign);
        sb.Append($"text-align: {align};");
        var color = Node.Style.Color ?? ts?.Color;
        if (color is not null) sb.Append($" color: {color};");
        var fontSize = Node.Style.FontSize ?? ts?.FontSize;
        if (fontSize is not null) sb.Append($" font-size: {F(fontSize.Value)}px;");
        var fontFamily = Node.Style.FontFamily ?? ts?.FontFamily;
        if (fontFamily is not null) sb.Append($" font-family: {fontFamily};");
        if (ts?.TextTransform is not null) sb.Append($" text-transform: {ts.TextTransform};");
        if (ts?.LetterSpacing is not null) sb.Append($" letter-spacing: {ts.LetterSpacing};");
        if (Node.Style.IsUnderline == true) sb.Append(" text-decoration: underline;");
        return sb.ToString();
    }

    private string GetTextInnerStyle(DiagramStencilTextStyle? ts)
    {
        var sb = new System.Text.StringBuilder();
        var isBold = Node.Style.IsBold ?? ts?.IsBold ?? false;
        var isItalic = Node.Style.IsItalic ?? ts?.IsItalic ?? false;
        if (isBold) sb.Append(" font-weight: 700;");
        if (isItalic) sb.Append(" font-style: italic;");
        return sb.ToString();
    }

    private static string GetTextAlign(StencilTextAlign? align)
        => align switch
        {
            StencilTextAlign.Center => "center",
            StencilTextAlign.Right => "right",
            _ => "left",
        };

    private static string GetVerticalAlignCss(string align)
        => align switch
        {
            "top" => "flex-start",
            "bottom" => "flex-end",
            _ => "center",
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
