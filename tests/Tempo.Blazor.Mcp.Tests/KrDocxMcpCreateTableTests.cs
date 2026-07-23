using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.DocumentFormats.Docx;
using Tempo.Blazor.DocumentFormats.Notion;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class KrDocxMcpCreateTableTests
{
    private static readonly Guid PageId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CreateTablePayloads_FromKrFixture_CompileToBothCanonicalTables()
    {
        await using var stream = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "KR.docx"));
        var imported = await new DocumentDocxImporter().ImportAsync(stream);
        var converted = DocumentModelToNotionConverter.ConvertDocument(
            imported.Document,
            PageId);
        var payload = BuildCreateTablePayload(converted.Blocks);
        var workingSet = WorkingSet();

        var compilation = await new NotionStrictOperationCompiler().CompileAsync(
            payload,
            workingSet,
            new NotionOperationCompileContext(
                "sha256:kr-docx-create-table",
                "kr-docx-create-table"),
            CancellationToken.None);

        compilation.Success.Should().BeTrue(
            string.Join(
                Environment.NewLine,
                compilation.Issues.Select(issue =>
                    $"{issue.Code} {issue.Path}: {issue.Message}")));
        compilation.Operations.Select(operation => operation.Apply(workingSet))
            .Should().OnlyContain(result => result.Success);
        NotionAggregateNormalizer.Normalize(workingSet);

        payload.Should().HaveCount(2);
        payload.Should().OnlyContain(node =>
            node != null &&
            node["op"]!.GetValue<string>() == "createTable" &&
            node["rows"] != null);

        var tables = workingSet.Pages[PageId].Blocks
            .Where(block => block.Type == BlockType.Table)
            .OrderBy(block => block.Order)
            .ToList();
        tables.Should().HaveCount(2);
        tables.Select(table =>
                table.Content.Deserialize<NotionAuthoringTable>(
                    NotionAggregateJson.Options)!.ColumnCount)
            .Should().Equal(8, 2);

        var firstRows = Rows(workingSet, tables[0].Id);
        var impactRows = Rows(workingSet, tables[1].Id);
        firstRows.Should().HaveCount(6);
        impactRows.Should().HaveCount(6);
        firstRows[2].Cells.Should().ContainSingle(cell =>
            cell.ColumnSpan == 7 && cell.RowSpan == 4);
        impactRows.SelectMany(row => row.Cells)
            .Select(cell => cell.BackgroundColor)
            .Should().Contain(color =>
                color != null &&
                color.Equals("#FF0000", StringComparison.OrdinalIgnoreCase));
        impactRows.SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Inlines)
            .Should().Contain(inline => inline.Bold);
    }

    private static JsonArray BuildCreateTablePayload(
        IReadOnlyList<IPageBlock> blocks)
    {
        var operations = new JsonArray();
        foreach (var tableBlock in blocks
                     .Where(block => block.Type == BlockType.Table)
                     .OrderBy(block => block.Order))
        {
            var table = (ITableBlockContent)tableBlock.Content;
            var rows = new JsonArray();
            foreach (var rowBlock in blocks
                         .Where(block =>
                             block.Type == BlockType.TableRow &&
                             block.ParentBlockId == tableBlock.Id)
                         .OrderBy(block => block.Order))
            {
                var row = (ITableRowBlockContent)rowBlock.Content;
                var cells = new JsonArray();
                foreach (var cell in row.RichCells.Where(cell =>
                             !cell.IsMergeHidden))
                {
                    cells.Add(JsonSerializer.SerializeToNode(
                        new NotionAuthoringTableCell
                        {
                            Html = cell.Html,
                            Inlines = cell.Inlines,
                            BackgroundColor = cell.BackgroundColor,
                            TextColor = cell.TextColor,
                            HorizontalAlignment = cell.HorizontalAlignment,
                            VerticalAlignment = cell.VerticalAlignment,
                            RowSpan = cell.RowSpan,
                            ColumnSpan = cell.ColSpan,
                            Width = cell.Width,
                            Borders = cell.Borders
                        },
                        NotionAggregateJson.Options));
                }
                rows.Add(new JsonObject { ["cells"] = cells });
            }

            operations.Add(new JsonObject
            {
                ["op"] = "createTable",
                ["clientRef"] = $"kr-table-{operations.Count + 1}",
                ["pageId"] = PageId.ToString("D"),
                ["order"] = tableBlock.Order,
                ["columnCount"] = table.ColumnCount,
                ["hasHeaderRow"] = table.HasHeaderRow,
                ["hasHeaderColumn"] = table.HasHeaderColumn,
                ["columnAlignments"] = JsonSerializer.SerializeToNode(
                    table.ColumnAlignments,
                    NotionAggregateJson.Options),
                ["columnWidths"] = JsonSerializer.SerializeToNode(
                    table.ColumnWidths,
                    NotionAggregateJson.Options),
                ["rows"] = rows
            });
        }

        return operations;
    }

    private static List<NotionAuthoringTableRow> Rows(
        NotionAggregateWorkingSet workingSet,
        Guid tableId)
        => workingSet.Pages[PageId].Blocks
            .Where(block =>
                block.Type == BlockType.TableRow &&
                block.ParentBlockId == tableId)
            .OrderBy(block => block.Order)
            .Select(block => block.Content.Deserialize<NotionAuthoringTableRow>(
                NotionAggregateJson.Options)!)
            .ToList();

    private static NotionAggregateWorkingSet WorkingSet()
        => new(new Dictionary<Guid, NotionPageSnapshot>
        {
            [PageId] = new NotionPageSnapshot
            {
                Page = new NotionPageState { Id = PageId, Title = "KR" },
                Blocks = [],
                ConcurrencyToken = "token-kr",
                Digest = "sha256:kr"
            }
        });
}
