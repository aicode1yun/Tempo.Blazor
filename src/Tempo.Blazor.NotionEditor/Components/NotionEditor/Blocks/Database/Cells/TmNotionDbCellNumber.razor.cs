using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellNumber : TmNotionDbCellBase
{
    private string        _buffer   = string.Empty;
    private ElementReference _inputRef;

    private NumberFormat Format =>
        (Field.Config as Tempo.Blazor.NotionEditor.Models.INumberFieldConfig)?.Format
        ?? NumberFormat.Number;

    private string DisplayValue => Value switch
    {
        double d => ApplyFormat(d),
        float  f => ApplyFormat(f),
        int    i => ApplyFormat(i),
        string s when double.TryParse(s, out var d) => ApplyFormat(d),
        _        => string.Empty
    };

    private string ApplyFormat(double v) => Format switch
    {
        NumberFormat.Dollar           => $"${v:N2}",
        NumberFormat.Euro             => $"€{v:N2}",
        NumberFormat.Pound            => $"£{v:N2}",
        NumberFormat.Yen              => $"¥{v:N0}",
        NumberFormat.Rupee            => $"₹{v:N2}",
        NumberFormat.Won              => $"₩{v:N0}",
        NumberFormat.Yuan             => $"¥{v:N2}",
        NumberFormat.Percent          => $"{v:P1}",
        NumberFormat.NumberWithCommas => v.ToString("N0"),
        _                             => v.ToString("G")
    };

    protected override void OnStartEdit()
    {
        _buffer = Value switch
        {
            double d => d.ToString("G"),
            float  f => f.ToString("G"),
            int    i => i.ToString(),
            string s => s,
            _        => string.Empty
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsEditing) try { await _inputRef.FocusAsync(); } catch { }
    }

    private async Task CommitNumberAsync()
    {
        object? val = double.TryParse(_buffer, out var d) ? d : (object?)null;
        await CommitAsync(val);
    }
}
