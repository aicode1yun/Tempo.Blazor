using Microsoft.AspNetCore.Components;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellPerson : TmNotionDbCellBase
{
    private string        _buffer   = string.Empty;
    private ElementReference _inputRef;

    protected override void OnStartEdit() => _buffer = StringValue;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsEditing) try { await _inputRef.FocusAsync(); } catch { }
    }
}
