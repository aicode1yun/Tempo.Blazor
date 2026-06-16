namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellCheckbox : TmNotionDbCellBase
{
    private bool IsChecked => Value switch
    {
        bool value => value,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True } => true,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False } => false,
        string value when bool.TryParse(value, out var parsed) => parsed,
        _ => false
    };

    private string AriaChecked => IsChecked ? "true" : "false";

    private async Task ToggleAsync()
    {
        if (ReadOnly) return;
        await CommitAsync(!IsChecked);
    }
}
