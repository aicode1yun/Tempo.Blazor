using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellSelect : TmNotionDbCellBase
{
    private string        _search    = string.Empty;
    private ElementReference _searchRef;

    private IReadOnlyList<SelectFieldOption> AllOptions =>
        (Field.Config as ISelectFieldConfig)?.Options ?? [];

    private IEnumerable<SelectFieldOption> FilteredOptions =>
        _search.Length == 0
            ? AllOptions
            : AllOptions.Where(o => o.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));

    private SelectFieldOption? _selectedOption =>
        AllOptions.FirstOrDefault(o =>
            string.Equals(o.Name, StringValue, StringComparison.OrdinalIgnoreCase));

    protected override void OnStartEdit()
    {
        _search = string.Empty;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsEditing) try { await _searchRef.FocusAsync(); } catch { }
    }

    private async Task HandleClickAsync()
    {
        if (!ReadOnly) await RequestEditAsync();
    }

    private async Task PickOptionAsync(string value)
    {
        await CommitAsync(value.Length > 0 ? value : null);
    }

    private async Task HandlePopupKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") await CancelAsync();
        else if (e.Key == "Enter")
        {
            var first = FilteredOptions.FirstOrDefault();
            if (first is not null) await PickOptionAsync(first.Name);
        }
    }
}
