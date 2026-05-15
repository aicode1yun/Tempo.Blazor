using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public class MockNotionSpreadsheetDocumentProvider : ISpreadsheetDocumentProvider
{
    private readonly Dictionary<Guid, SpreadsheetWorkbook> _store = new();

    public Task<(Guid Id, SpreadsheetWorkbook Workbook)> CreateSpreadsheetDocumentAsync(string title)
    {
        var id       = Guid.NewGuid();
        var workbook = new SpreadsheetWorkbook();
        _store[id]   = workbook;
        return Task.FromResult((id, workbook));
    }

    public Task<SpreadsheetWorkbook?> GetSpreadsheetDocumentAsync(Guid documentId)
    {
        _store.TryGetValue(documentId, out var workbook);
        return Task.FromResult(workbook);
    }

    public Task<SpreadsheetWorkbook> SaveSpreadsheetDocumentAsync(Guid documentId, SpreadsheetWorkbook workbook)
    {
        _store[documentId] = workbook;
        return Task.FromResult(workbook);
    }
}
