using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionTableGridProjectionTests
{
    [Fact]
    public void TryProject_RowAndColumnSpans_ProducesCompletePhysicalGrid()
    {
        IReadOnlyList<NotionAuthoringTableRow> rows =
        [
            Row(
                Cell("A", rowSpan: 2),
                Cell("B", columnSpan: 2)),
            Row(
                Cell("C"),
                Cell("D"))
        ];

        var success = NotionTableGridProjector.TryProject(
            rows,
            3,
            "$.rows",
            out var projection,
            out var issues);

        success.Should().BeTrue();
        issues.Should().BeEmpty();
        projection.Should().NotBeNull();
        projection!.Slots.Should().HaveCount(6);
        projection.GetSlot(0, 0).Should().Match<NotionTableGridSlot>(
            slot => slot.IsOrigin && slot.OriginRow == 0 && slot.OriginColumn == 0);
        projection.GetSlot(1, 0).Should().Match<NotionTableGridSlot>(
            slot => !slot.IsOrigin && slot.OriginRow == 0 && slot.OriginColumn == 0);
        projection.GetSlot(0, 2).Should().Match<NotionTableGridSlot>(
            slot => !slot.IsOrigin && slot.OriginRow == 0 && slot.OriginColumn == 1);
        projection.GetSlot(1, 1).Cell.Html.Should().Be("C");
        projection.GetSlot(1, 2).Cell.Html.Should().Be("D");
    }

    [Fact]
    public void TryProject_SpanCrossesAnActiveRowSpan_ReturnsStableOverlapDiagnostic()
    {
        IReadOnlyList<NotionAuthoringTableRow> rows =
        [
            Row(
                Cell("A", rowSpan: 2),
                Cell("B"),
                Cell("C", rowSpan: 2)),
            Row(Cell("D", columnSpan: 2))
        ];

        var success = NotionTableGridProjector.TryProject(
            rows,
            3,
            "$.operations[0].rows",
            out var projection,
            out var issues);

        success.Should().BeFalse();
        projection.Should().BeNull();
        issues.Should().ContainSingle(issue =>
            issue.Code == "table_cell_overlap" &&
            issue.Severity == NotionIssueSeverity.Error &&
            issue.Path == "$.operations[0].rows[1].cells[0].columnSpan" &&
            !string.IsNullOrWhiteSpace(issue.SuggestedFix));
    }

    [Fact]
    public void TryProject_RowSpanOverflowsRows_ReturnsExactPath()
    {
        var success = NotionTableGridProjector.TryProject(
            [Row(Cell("A", rowSpan: 2))],
            1,
            "$.rows",
            out _,
            out var issues);

        success.Should().BeFalse();
        issues.Should().ContainSingle(issue =>
            issue.Code == "table_row_span_overflow" &&
            issue.Path == "$.rows[0].cells[0].rowSpan");
    }

    [Fact]
    public void TryProject_RowDoesNotCoverWidth_ReturnsExactPath()
    {
        var success = NotionTableGridProjector.TryProject(
            [Row(Cell("A"))],
            2,
            "$.rows",
            out _,
            out var issues);

        success.Should().BeFalse();
        issues.Should().ContainSingle(issue =>
            issue.Code == "table_row_width_mismatch" &&
            issue.Path == "$.rows[0].cells");
    }

    [Fact]
    public void TryProject_ResourceLimitsFailBeforeAllocatingUnboundedGrid()
    {
        var rows = Enumerable.Range(0, NotionAuthoringLimits.MaxTableRows + 1)
            .Select(_ => Row(Cell("x")))
            .ToList();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var success = NotionTableGridProjector.TryProject(
            rows,
            1,
            "$.rows",
            out _,
            out var issues);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        success.Should().BeFalse();
        issues.Should().ContainSingle(issue =>
            issue.Code == "table_row_limit_exceeded" &&
            issue.Path == "$.rows");
        allocated.Should().BeLessThan(4 * 1024 * 1024);
    }

    [Fact]
    public void TryProject_DeterministicFuzzStaysWithinTimeAndMemoryBudget()
    {
        var random = new Random(0x2700);
        var stopwatch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var rowCount = random.Next(1, 16);
            var columnCount = random.Next(1, 12);
            var rows = new List<NotionAuthoringTableRow>(rowCount);
            for (var row = 0; row < rowCount; row++)
            {
                var cells = Enumerable.Range(0, random.Next(0, columnCount + 3))
                    .Select(_ => Cell(
                        "x",
                        rowSpan: random.Next(1, 5),
                        columnSpan: random.Next(1, 5)))
                    .ToList();
                rows.Add(new NotionAuthoringTableRow { Cells = cells });
            }

            _ = NotionTableGridProjector.TryProject(
                rows,
                columnCount,
                "$.rows",
                out _,
                out _);
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        allocated.Should().BeLessThan(128 * 1024 * 1024);
    }

    [Fact]
    public void AggregateValidator_RejectsOrphanRowsWrongChildrenAndAlignmentCount()
    {
        var pageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tableId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var page = new NotionPageSnapshot
        {
            Page = new NotionPageState { Id = pageId },
            Blocks =
            [
                Block(
                    tableId,
                    pageId,
                    BlockType.Table,
                    0,
                    new NotionAuthoringTable
                    {
                        ColumnCount = 2,
                        ColumnAlignments = [NotionTableHorizontalAlignment.Left]
                    }),
                Block(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    pageId,
                    BlockType.Paragraph,
                    0,
                    new { html = "wrong child" },
                    tableId),
                Block(
                    Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    pageId,
                    BlockType.TableRow,
                    1,
                    new NotionAuthoringTableRow
                    {
                        Cells = [Cell("A"), Cell("B")]
                    })
            ]
        };

        var issues = NotionAggregateValidator.Validate([page]);

        issues.Should().Contain(issue =>
            issue.Code == "table_column_alignment_count_mismatch" &&
            issue.Path == "$.pages[0].blocks[0].content.columnAlignments" &&
            !string.IsNullOrWhiteSpace(issue.SuggestedFix));
        issues.Should().Contain(issue =>
            issue.Code == "table_child_type_invalid" &&
            issue.Path == "$.pages[0].blocks[1].type");
        issues.Should().Contain(issue =>
            issue.Code == "table_row_parent_required" &&
            issue.Path == "$.pages[0].blocks[2].parentBlockId");
    }

    [Fact]
    public void AggregateValidator_RejectsUnsafeStoredTableCellContentAndColor()
    {
        var pageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tableId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var page = new NotionPageSnapshot
        {
            Page = new NotionPageState { Id = pageId },
            Blocks =
            [
                Block(
                    tableId,
                    pageId,
                    BlockType.Table,
                    0,
                    new NotionAuthoringTable { ColumnCount = 1 }),
                Block(
                    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    pageId,
                    BlockType.TableRow,
                    0,
                    new NotionAuthoringTableRow
                    {
                        Cells =
                        [
                            new NotionAuthoringTableCell
                            {
                                Html = """<img src=x onerror="alert(1)">""",
                                BackgroundColor = "red;position:fixed"
                            }
                        ]
                    },
                    tableId)
            ]
        };

        var issues = NotionAggregateValidator.Validate([page]);

        issues.Should().Contain(issue =>
            issue.Code == "unsafe_table_cell_html" &&
            issue.Path == "$.pages[0].blocks[1].content.cells[0].html");
        issues.Should().Contain(issue =>
            issue.Code == "unsafe_css_color" &&
            issue.Path == "$.pages[0].blocks[1].content.cells[0].backgroundColor");
    }

    [Theory]
    [InlineData("""<img src=x onerror="alert(1)">""", "unsafe_table_cell_html", ".html")]
    [InlineData("""<span style="color:url(javascript:alert(1))">x</span>""", "unsafe_table_cell_html", ".html")]
    [InlineData("safe", "unsafe_css_color", ".backgroundColor", "url(https://evil.test/x)")]
    [InlineData("safe", "unsafe_css_color", ".backgroundColor", "var(--evil)")]
    [InlineData("safe", "unsafe_css_color", ".backgroundColor", "red;position:fixed")]
    public async Task StrictCompiler_RejectsMaliciousHtmlAndCss(
        string html,
        string code,
        string pathSuffix,
        string? backgroundColor = null)
    {
        var page = TestPage();
        var compiler = new NotionStrictOperationCompiler();
        var operations = $$"""
            [{
              "op":"createTable",
              "pageId":"{{page.Page.Id}}",
              "columnCount":1,
              "rows":[{"cells":[{
                "html":{{JsonSerializer.Serialize(html)}}{{(backgroundColor is null ? string.Empty : $""","backgroundColor":{JsonSerializer.Serialize(backgroundColor)}""")}}
              }]}]
            }]
            """;

        var result = await compiler.CompileAsync(
            JsonNode.Parse(operations)!.AsArray(),
            WorkingSet(page),
            new NotionOperationCompileContext("security", "sha256:test"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Issues.Should().Contain(issue =>
            issue.Code == code &&
            issue.Path == "$.operations[0].rows[0].cells[0]" + pathSuffix &&
            issue.Severity == NotionIssueSeverity.Error &&
            !string.IsNullOrWhiteSpace(issue.SuggestedFix));
    }

    private static NotionAuthoringTableRow Row(params NotionAuthoringTableCell[] cells)
        => new() { Cells = cells };

    private static NotionAuthoringTableCell Cell(
        string html,
        int rowSpan = 1,
        int columnSpan = 1)
        => new()
        {
            Html = html,
            RowSpan = rowSpan,
            ColumnSpan = columnSpan
        };

    private static NotionBlockSnapshot Block(
        Guid id,
        Guid pageId,
        BlockType type,
        int order,
        object content,
        Guid? parentBlockId = null)
        => new()
        {
            Id = id,
            PageId = pageId,
            ParentBlockId = parentBlockId,
            Type = type,
            Order = order,
            Content = JsonSerializer.SerializeToElement(content, NotionAggregateJson.Options)
        };

    private static NotionPageSnapshot TestPage()
        => new()
        {
            Page = new NotionPageState
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111")
            },
            ConcurrencyToken = "token"
        };

    private static NotionAggregateWorkingSet WorkingSet(NotionPageSnapshot page)
        => new(new Dictionary<Guid, NotionPageSnapshot>
        {
            [page.Page.Id] = page
        });
}
