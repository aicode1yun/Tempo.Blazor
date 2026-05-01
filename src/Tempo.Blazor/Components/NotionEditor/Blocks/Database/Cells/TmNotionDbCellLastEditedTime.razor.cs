namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database.Cells;

public partial class TmNotionDbCellLastEditedTime : TmNotionDbCellBase
{
    private DateTime? Dt => Value switch
    {
        DateTime dt => dt,
        string s when DateTime.TryParse(s, out var d) => d,
        _ => null
    };

    private string DisplayValue => Dt is { } dt ? dt.ToString("MMM d, yyyy") : string.Empty;
    private string FullDateTime => Dt is { } dt ? dt.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;
}
