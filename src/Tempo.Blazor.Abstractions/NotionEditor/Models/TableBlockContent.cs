namespace Tempo.Blazor.NotionEditor.Models;

public class TableBlockContent : ITableBlockContent
{
    public bool HasHeaderRow { get; set; }
    public bool HasHeaderColumn { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasColumnHeader
    {
        get => HasHeaderRow;
        set => HasHeaderRow = value;
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasRowHeader
    {
        get => HasHeaderColumn;
        set => HasHeaderColumn = value;
    }
    public int ColumnCount { get; set; }
}
