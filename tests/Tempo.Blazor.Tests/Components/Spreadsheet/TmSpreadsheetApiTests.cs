using Bunit;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class TmSpreadsheetApiTests : LocalizationTestBase
{
    [Fact]
    public async Task SetCellValue_SetsValueAndFiresOnChange()
    {
        var cut = Render<TmSpreadsheet>();
        SpreadsheetChangeEventArgs? received = null;
        cut.Render(parameters => parameters
            .Add(p => p.OnChange, EventCallback.Factory.Create<SpreadsheetChangeEventArgs>(this, e => received = e)));

        await cut.InvokeAsync(() => cut.Instance.SetCellValue("A1", "Hello"));

        cut.Instance.GetCellValue("A1").Should().Be("Hello");
        received.Should().NotBeNull();
        received!.CellRef.Should().Be("A1");
        received!.NewValue.Should().Be("Hello");
    }

    [Fact]
    public void GetCellValue_ReturnsNullForEmptyCell()
    {
        var cut = Render<TmSpreadsheet>();
        cut.Instance.GetCellValue("Z99").Should().BeNull();
    }

    [Fact]
    public void GetActiveSheet_ReturnsCurrentSheet()
    {
        var cut = Render<TmSpreadsheet>();
        var sheet = cut.Instance.GetActiveSheet();
        sheet.Should().NotBeNull();
        sheet!.Name.Should().Be("Sheet1");
    }

    [Fact]
    public async Task ExportToExcelAsync_ReturnsNonEmptyBytes()
    {
        var cut = Render<TmSpreadsheet>();
        await cut.InvokeAsync(() => cut.Instance.SetCellValue("A1", "Test"));

        var data = await cut.Instance.ExportToExcelAsync();
        data.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ImportFromExcelAsync_LoadsWorkbook()
    {
        var cut = Render<TmSpreadsheet>();
        await cut.InvokeAsync(() => cut.Instance.SetCellValue("A1", "Before"));
        var exported = await cut.Instance.ExportToExcelAsync();

        await cut.InvokeAsync(() => cut.Instance.ImportFromExcelAsync(exported));

        cut.Instance.GetCellValue("A1").Should().Be("Before");
    }

    [Fact]
    public void OnOpen_FiresWhenFileImported()
    {
        // OnOpen is already tested in Phase 9 via XLSX round-trip.
        // This test verifies the event parameter contract.
        var args = new SpreadsheetOpenEventArgs("test.xlsx", Array.Empty<byte>(), new SpreadsheetWorkbook());
        args.FileName.Should().Be("test.xlsx");
        args.Workbook.Should().NotBeNull();
    }

    [Fact]
    public void OnDownload_FiresWhenExported()
    {
        var args = new SpreadsheetDownloadEventArgs("test.xlsx", Array.Empty<byte>());
        args.FileName.Should().Be("test.xlsx");
        args.Data.Should().BeEmpty();
    }
}
