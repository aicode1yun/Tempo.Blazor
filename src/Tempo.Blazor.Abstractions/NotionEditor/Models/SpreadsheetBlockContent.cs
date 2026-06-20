namespace Tempo.Blazor.NotionEditor.Models;

public class SpreadsheetBlockContent : ISpreadsheetBlockContent
{
    public Guid SpreadsheetDocumentId { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Caption { get; set; }
}
