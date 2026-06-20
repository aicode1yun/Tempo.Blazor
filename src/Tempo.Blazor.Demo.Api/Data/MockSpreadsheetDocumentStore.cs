using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Demo.Api.Data;

public class MockSpreadsheetDocumentStore
{
    private readonly Dictionary<Guid, SpreadsheetWorkbook> _store = new();

    public (Guid Id, SpreadsheetWorkbook Workbook) Create()
    {
        var id       = Guid.NewGuid();
        var workbook = new SpreadsheetWorkbook();
        _store[id]   = workbook;
        return (id, workbook);
    }

    public SpreadsheetWorkbook? Get(Guid id)
    {
        _store.TryGetValue(id, out var w);
        return w;
    }

    public SpreadsheetWorkbook Save(Guid id, SpreadsheetWorkbook workbook)
    {
        _store[id] = workbook;
        return workbook;
    }
}
