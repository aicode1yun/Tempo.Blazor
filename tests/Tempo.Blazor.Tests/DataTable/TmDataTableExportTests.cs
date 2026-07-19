using System.Text;
using Bunit;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Rendering;
using Tempo.Blazor.Components.DataTable;
using Tempo.Blazor.Export;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.DataTable;

/// <summary>Export: CSV exporter, XLSX exporter, and the component's all-rows export snapshot.</summary>
public class TmDataTableExportTests : LocalizationTestBase
{
    public record Person(string Name, int Age);

    // ── CSV exporter ──────────────────────────────────────────────────────────

    [Fact]
    public void Csv_QuotesFieldsWithDelimitersAndNewlines()
    {
        var exporter = new CsvDataTableExporter(writeBom: false);
        var data = new DataTableExportData
        {
            Headers = ["Name", "Note"],
            Rows = [["Ann", "hi, there"], ["Bob", "line\nbreak"], ["Quote", "she said \"hi\""]]
        };

        var text = Encoding.UTF8.GetString(exporter.Export(data));

        text.Should().Contain("Name,Note");
        text.Should().Contain("\"hi, there\"");
        text.Should().Contain("\"line\nbreak\"");
        text.Should().Contain("\"she said \"\"hi\"\"\"");
    }

    [Fact]
    public void Csv_WritesBomWhenRequested()
    {
        var bytes = new CsvDataTableExporter(writeBom: true).Export(new DataTableExportData { Headers = ["A"], Rows = [] });
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
    }

    // ── XLSX exporter ─────────────────────────────────────────────────────────

    [Fact]
    public void Xlsx_ProducesWellFormedWorkbook()
    {
        var exporter = new XlsxDataTableExporter();
        exporter.FileExtension.Should().Be("xlsx");

        var bytes = exporter.Export(new DataTableExportData
        {
            Name = "People",
            Headers = ["Name", "Age"],
            Rows = [["Ann", "30"], ["Bob", "25"]]
        });

        bytes.Length.Should().BeGreaterThan(0);

        using var ms = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(ms, false);
        var worksheet = doc.WorkbookPart!.WorksheetParts.First().Worksheet;
        var rows = worksheet.Descendants<Row>().ToList();
        rows.Should().HaveCount(3); // header + 2 data rows
        var firstCell = rows[0].Descendants<Cell>().First();
        firstCell.InnerText.Should().Contain("Name");
    }

    // ── Component export snapshot ─────────────────────────────────────────────

    private IRenderedComponent<TmDataTable<Person>> RenderTable(List<Person> items, int pageSize = 10)
        => Render<TmDataTable<Person>>(p =>
        {
            p.Add(c => c.ViewContext, "export-test");
            p.Add(c => c.Items, items);
            p.Add(c => c.DefaultPageSize, pageSize);
            p.AddChildContent(b =>
            {
                var seq = 0;
                AddCol(b, ref seq, "Name", x => x.Name);
                AddCol(b, ref seq, "Age", x => x.Age);
            });
        });

    private static void AddCol(RenderTreeBuilder b, ref int seq, string title, Func<Person, object?> field)
    {
        b.OpenComponent<TmDataTableColumn<Person>>(seq++);
        b.AddAttribute(seq++, "Title", title);
        b.AddAttribute(seq++, "PropertyName", title);
        b.AddAttribute(seq++, "Sortable", true);
        b.AddAttribute(seq++, "Field", field);
        b.CloseComponent();
    }

    [Fact]
    public async Task BuildExportData_ReturnsAllRows_NotJustCurrentPage()
    {
        var items = Enumerable.Range(1, 50).Select(i => new Person($"P{i:D2}", i)).ToList();
        var cut = RenderTable(items, pageSize: 10);
        await cut.InvokeAsync(() => { });

        DataTableExportData? data = null;
        await cut.InvokeAsync(async () => data = await cut.Instance.BuildExportDataAsync());

        data.Should().NotBeNull();
        data!.Headers.Should().Equal("Name", "Age");
        data.Rows.Should().HaveCount(50); // all pages, not the 10-row page
    }

    [Fact]
    public async Task BuildExportData_RespectsCurrentSort()
    {
        var items = new List<Person> { new("C", 3), new("A", 1), new("B", 2) };
        var cut = RenderTable(items);
        await cut.InvokeAsync(() => { });

        cut.FindAll("th[data-sortable='true']")[0].Click(); // sort by Name asc

        DataTableExportData? data = null;
        await cut.InvokeAsync(async () => data = await cut.Instance.BuildExportDataAsync());

        data!.Rows.Select(r => r[0]).Should().Equal("A", "B", "C");
    }

    [Fact]
    public async Task ExportAsync_DoesNotThrow_WhenJsUnavailable()
    {
        var cut = RenderTable([new Person("Ann", 30)]);
        await cut.InvokeAsync(() => { });

        var act = async () => await cut.InvokeAsync(() => cut.Instance.ExportAsync(new CsvDataTableExporter()));
        await act.Should().NotThrowAsync();
    }
}
