using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Clipboard.Normalizers;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor.Clipboard;

public sealed class GoogleSheetsClipboardNormalizerTests
{
    private static GoogleSheetsClipboardNormalizer Create() => new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DocumentEditor", "Clipboard", name);
        return File.ReadAllText(path);
    }

    // ─── CanHandle ────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_GoogleSheetsMarker_ReturnsTrue()
    {
        var html = "<google-sheets-html-origin><table><tr><td>A</td></tr></table></google-sheets-html-origin>";
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_SheetsDataAttribute_ReturnsTrue()
    {
        var html = "<table><tr><td data-sheets-value='{\"1\":2}'>Val</td></tr></table>";
        Assert.True(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_RegularTable_ReturnsFalse()
    {
        var html = "<table><tr><td>Normal</td></tr></table>";
        Assert.False(Create().CanHandle(new DocumentClipboardInput { Html = html }));
    }

    [Fact]
    public void CanHandle_TsvPlainText_ReturnsTrue()
    {
        Assert.True(Create().CanHandle(new DocumentClipboardInput { PlainText = "A\tB\nC\tD" }));
    }

    [Fact]
    public void CanHandle_NoHtmlOrTsv_ReturnsFalse()
    {
        Assert.False(Create().CanHandle(new DocumentClipboardInput { PlainText = "Normal text" }));
    }

    // ─── google-sheets-table.html fixture ────────────────────────────────────

    [Fact]
    public void Normalize_GoogleSheetsFixture_CreatesTableBlock()
    {
        var html = LoadFixture("google-sheets-table.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var tableBlock = output.Blocks.FirstOrDefault(b => b.Type == DocumentBlockType.Table);
        Assert.NotNull(tableBlock);
        var table = Assert.IsType<TableBlockContent>(tableBlock.Content);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(3, table.Rows[0].Cells.Count);
    }

    [Fact]
    public void Normalize_GoogleSheetsFixture_CellsContainText()
    {
        var html = LoadFixture("google-sheets-table.html");
        var output = Create().Normalize(new DocumentClipboardInput { Html = html });

        var table = Assert.IsType<TableBlockContent>(output.Blocks.First(b => b.Type == DocumentBlockType.Table).Content);
        var firstCellText = GetCellText(table.Rows[0].Cells[0]);
        Assert.Contains("Name", firstCellText, StringComparison.OrdinalIgnoreCase);
    }

    // ─── TSV fallback ────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_TsvFallback_CreatesTableBlock()
    {
        var tsv = "Name\tScore\nAlice\t95\nBob\t82";
        var output = Create().Normalize(new DocumentClipboardInput { PlainText = tsv });

        var tableBlock = output.Blocks.FirstOrDefault(b => b.Type == DocumentBlockType.Table);
        Assert.NotNull(tableBlock);
        var table = Assert.IsType<TableBlockContent>(tableBlock.Content);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(2, table.Rows[0].Cells.Count);
    }

    [Fact]
    public void Normalize_TsvFallback_CellsContainCorrectText()
    {
        var tsv = "Hello\tWorld";
        var output = Create().Normalize(new DocumentClipboardInput { PlainText = tsv });

        var table = Assert.IsType<TableBlockContent>(output.Blocks[0].Content);
        Assert.Equal("Hello", GetCellText(table.Rows[0].Cells[0]));
        Assert.Equal("World", GetCellText(table.Rows[0].Cells[1]));
    }

    private static string GetCellText(TableCellContent cell) =>
        string.Concat(
            cell.Blocks
                .SelectMany(b => (b.Content as ParagraphBlockContent)?.Inlines.OfType<TextRun>().Select(r => r.Text) ?? []));
}
