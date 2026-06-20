namespace Tempo.Blazor.Components.Spreadsheet.Models;

/// <summary>Response returned after creating a persisted spreadsheet document.</summary>
public sealed record SpreadsheetDocumentCreateResult(Guid Id, SpreadsheetWorkbook Workbook);
