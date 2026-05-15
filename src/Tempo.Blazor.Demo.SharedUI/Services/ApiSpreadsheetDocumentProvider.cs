using System.Net.Http.Json;
using System.Text.Json;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Demo.Services;

public class ApiSpreadsheetDocumentProvider : ISpreadsheetDocumentProvider
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiSpreadsheetDocumentProvider(IHttpClientFactory factory)
        => _http = factory.CreateClient("DemoApi");

    public async Task<(Guid Id, SpreadsheetWorkbook Workbook)> CreateSpreadsheetDocumentAsync(string title)
    {
        var response = await _http.PostAsync("/api/notion/spreadsheets", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateResult>(_json);
        return (result!.Id, result.Workbook);
    }

    public async Task<SpreadsheetWorkbook?> GetSpreadsheetDocumentAsync(Guid documentId)
    {
        var response = await _http.GetAsync($"/api/notion/spreadsheets/{documentId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpreadsheetWorkbook>(_json);
    }

    public async Task<SpreadsheetWorkbook> SaveSpreadsheetDocumentAsync(Guid documentId, SpreadsheetWorkbook workbook)
    {
        var response = await _http.PutAsJsonAsync($"/api/notion/spreadsheets/{documentId}", workbook, _json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpreadsheetWorkbook>(_json))!;
    }

    private sealed record CreateResult(Guid Id, SpreadsheetWorkbook Workbook);
}
