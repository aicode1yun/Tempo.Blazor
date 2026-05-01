namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellCheckbox : TmNotionDbCellBase
{
    private bool IsChecked => Value is bool b && b;

    private async Task ToggleAsync()
    {
        if (ReadOnly) return;
        await CommitAsync(!IsChecked);
    }
}
