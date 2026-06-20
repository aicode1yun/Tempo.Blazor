namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.Components.Spreadsheet.Models;

public interface ISpreadsheetDocumentProvider
{
    Task<SpreadsheetWorkbook?> GetSpreadsheetDocumentAsync(Guid documentId);
    Task<SpreadsheetWorkbook> SaveSpreadsheetDocumentAsync(Guid documentId, SpreadsheetWorkbook workbook);
    Task<(Guid Id, SpreadsheetWorkbook Workbook)> CreateSpreadsheetDocumentAsync(string title);
}
