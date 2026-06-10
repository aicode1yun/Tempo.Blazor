using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public class ApiSpreadsheetDocumentProvider : ISpreadsheetDocumentProvider
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public ApiSpreadsheetDocumentProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<(Guid Id, SpreadsheetWorkbook Workbook)> CreateSpreadsheetDocumentAsync(string title)
    {
        var response = await _http.PostAsync("/api/notion/spreadsheets", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SpreadsheetDocumentCreateResult>(_json);
        return (result!.Id, NormalizeWorkbook(result.Workbook));
    }

    public async Task<SpreadsheetWorkbook?> GetSpreadsheetDocumentAsync(Guid documentId)
    {
        var response = await _http.GetAsync($"/api/notion/spreadsheets/{documentId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var workbook = await response.Content.ReadFromJsonAsync<SpreadsheetWorkbook>(_json);
        return NormalizeWorkbook(workbook);
    }

    public async Task<SpreadsheetWorkbook> SaveSpreadsheetDocumentAsync(Guid documentId, SpreadsheetWorkbook workbook)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/spreadsheets/{documentId}", NormalizeWorkbook(workbook), _json);
        response.EnsureSuccessStatusCode();
        return NormalizeWorkbook(await response.Content.ReadFromJsonAsync<SpreadsheetWorkbook>(_json));
    }

    private static SpreadsheetWorkbook NormalizeWorkbook(SpreadsheetWorkbook? workbook)
    {
        var normalized = workbook ?? new SpreadsheetWorkbook();
        if (normalized.Sheets.Count == 0)
            normalized.AddSheet("Sheet1");

        if (normalized.ActiveSheetIndex < 0 || normalized.ActiveSheetIndex >= normalized.Sheets.Count)
            normalized.ActiveSheetIndex = 0;

        for (var i = 0; i < normalized.Sheets.Count; i++)
        {
            normalized.Sheets[i].Workbook = normalized;
            normalized.Sheets[i].SheetIndexInWorkbook = i;
        }

        return normalized;
    }
}
