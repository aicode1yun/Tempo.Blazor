using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellStatus : TmNotionDbCellBase
{
    private string        _search    = string.Empty;
    private ElementReference _searchRef;

    private IReadOnlyList<StatusGroup> AllGroups =>
        (Field.Config as IStatusFieldConfig)?.Groups ?? [];

    private IEnumerable<StatusGroup> FilteredGroups =>
        _search.Length == 0
            ? AllGroups
            : AllGroups.Select(g => new StatusGroup(
                g.Name, g.Color,
                g.Options.Where(o => o.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList()))
              .Where(g => g.Options.Count > 0);

    private SelectFieldOption? _selectedOption => AllGroups
        .SelectMany(g => g.Options)
        .FirstOrDefault(o => string.Equals(o.Name, StringValue, StringComparison.OrdinalIgnoreCase));

    protected override void OnStartEdit() => _search = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsEditing) try { await _searchRef.FocusAsync(); } catch { }
    }

    private async Task HandleClickAsync()
    {
        if (!ReadOnly) await RequestEditAsync();
    }

    private async Task PickAsync(string name) => await CommitAsync(name);

    private async Task HandlePopupKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") await CancelAsync();
        else if (e.Key == "Enter")
        {
            var first = AllGroups.SelectMany(g => g.Options)
                .FirstOrDefault(o => o.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));
            if (first is not null) await PickAsync(first.Name);
        }
    }
}
