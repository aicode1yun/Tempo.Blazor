namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellRelation : TmNotionDbCellBase
{
    private IReadOnlyList<string> LinkedIds => Value switch
    {
        string[] arr             => arr,
        IEnumerable<string> list => list.ToList(),
        string s when s.Length > 0 => s.Split(',', StringSplitOptions.RemoveEmptyEntries),
        _                        => []
    };
}
