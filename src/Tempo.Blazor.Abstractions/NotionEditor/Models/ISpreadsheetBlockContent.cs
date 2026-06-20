namespace Tempo.Blazor.NotionEditor.Models;

public interface ISpreadsheetBlockContent : IBlockContent
{
    Guid SpreadsheetDocumentId { get; }
    int? Width { get; }
    int? Height { get; }
    string? Caption { get; }
}
