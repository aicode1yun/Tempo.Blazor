using System.Text.Json;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Blocks.Table;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionTableAdvancedTests : LocalizationTestBase
{
    public TmNotionTableAdvancedTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["TmNotionTableBlock_Loading"] = "Loading",
            ["TmNotionTableBlock_TableLabel"] = "Table",
            ["TmNotionTableBlock_AddRow"] = "New row",
            ["TmNotionTableBlock_AddColumn"] = "Add column",
            ["TmNotionTableBlock_DeleteColumn"] = "Delete column",
            ["TmNotionTableBlock_DeleteRow"] = "Delete row",
            ["TmNotionTableBlock_DragRow"] = "Drag row",
            ["TmNotionTableBlock_ToggleHeaderRow"] = "Header row",
            ["TmNotionTableBlock_ToggleHeaderColumn"] = "Header column",
            ["TmNotionTableBlock_SelectionTools"] = "Table selection tools",
            ["TmNotionTableBlock_Merge"] = "Merge cells",
            ["TmNotionTableBlock_Split"] = "Split",
            ["TmNotionTableBlock_Undo"] = "Undo table change",
            ["TmNotionTableBlock_ClearColor"] = "Clear color",
            ["TmNotionTableBlock_ColorYellow"] = "Yellow",
            ["TmNotionTableBlock_ColorGreen"] = "Green",
            ["TmNotionTableBlock_ColorBlue"] = "Blue",
            ["TmNotionTableBlock_ColorRed"] = "Red",
            ["TmNotionTableBlock_Sort"] = "Sort column"
        });
    }

    [Fact]
    public void TableRowContent_SerializesRichCells_AndKeepsLegacyCellsCompatible()
    {
        var content = new TableRowBlockContent
        {
            Cells = ["Legacy"],
            RichCells =
            [
                new NotionTableCell
                {
                    Html = "Merged",
                    ColSpan = 2,
                    RowSpan = 3,
                    BackgroundColor = "var(--tm-color-warning-bg)"
                }
            ]
        };

        var json = JsonSerializer.Serialize(content);
        var restored = JsonSerializer.Deserialize<TableRowBlockContent>(json);

        restored.Should().NotBeNull();
        restored!.Cells.Should().ContainSingle().Which.Should().Be("Legacy");
        restored.RichCells.Should().ContainSingle().Which.Should().BeEquivalentTo(content.RichCells[0]);

        var legacy = JsonSerializer.Deserialize<TableRowBlockContent>("""
            {"Cells":["A","B"]}
            """);

        legacy!.Cells.Should().Equal("A", "B");
        legacy.RichCells.Should().BeEmpty();
    }

    [Fact]
    public void TableGridValidator_RejectsMergedCellOverlap()
    {
        var grid = new List<IReadOnlyList<NotionTableCell>>
        {
            new List<NotionTableCell>
            {
                new() { Html = "A", ColSpan = 2 },
                new() { Html = "B" }
            }
        };

        var valid = NotionTableGridValidator.TryValidate(grid, 2, out var errors);

        valid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("overlaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TableBlockContent_SerializesOnlyCanonicalHeaderKeys_AndAliasesRoundtrip()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var content = new TableBlockContent
        {
            HasHeaderRow = true,
            HasHeaderColumn = false,
            ColumnCount = 3
        };

        var json = JsonSerializer.Serialize(content, options);

        json.Should().Contain("hasHeaderRow");
        json.Should().Contain("hasHeaderColumn");
        json.Should().NotContain("hasColumnHeader");
        json.Should().NotContain("hasRowHeader");

        var restored = JsonSerializer.Deserialize<TableBlockContent>("""
            {"hasHeaderRow":true,"hasHeaderColumn":false,"columnCount":3}
            """, options);

        restored.Should().NotBeNull();
        restored!.HasHeaderRow.Should().BeTrue();
        restored.HasColumnHeader.Should().BeTrue();
        restored.HasHeaderColumn.Should().BeFalse();
        restored.HasRowHeader.Should().BeFalse();
    }

    [Fact]
    public void TableGridValidator_RejectsNullRowsWithoutThrowing()
    {
        var valid = NotionTableGridValidator.TryValidate(null, 3, out var errors);

        valid.Should().BeFalse();
        errors.Should().ContainSingle(error => error.Contains("Rows cannot be null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TableGridValidator_ReportsSingleOverlapErrorPerCell()
    {
        var grid = new List<IReadOnlyList<NotionTableCell>>
        {
            new List<NotionTableCell>
            {
                new() { Html = "A", ColSpan = 2, RowSpan = 2 },
                new() { Html = "B", ColSpan = 2, RowSpan = 2 }
            },
            new List<NotionTableCell>
            {
                new() { Html = "C" },
                new() { Html = "D" }
            }
        };

        var valid = NotionTableGridValidator.TryValidate(grid, 3, out var errors);

        valid.Should().BeFalse();
        errors.Count(error => error.Contains("Cell 0:1 overlaps", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    [Fact]
    public void TableGridValidator_RejectsOrphanHiddenMergeSlot()
    {
        var grid = new List<IReadOnlyList<NotionTableCell>>
        {
            new List<NotionTableCell>
            {
                new() { Html = "Visible" },
                new() { IsMergeHidden = true }
            }
        };

        var valid = NotionTableGridValidator.TryValidate(grid, 2, out var errors);

        valid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("Hidden cell 0:1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Table_MergeSplitColorAndSort_PersistsThroughBlockProvider()
    {
        var provider = new TableBlockProvider();
        var cut = RenderTable(provider);

        cut.WaitForAssertion(() => cut.FindAll(".tm-notion-table__cell-td").Should().HaveCount(9));

        var table = cut.FindComponent<TmNotionTableBlock>();
        await cut.InvokeAsync(() => table.Instance.SetTableSelection(1, 0, 2, 1));
        cut.Find("button[title='Merge cells']").Click();

        provider.Rows[1].Content.Should().BeOfType<TableRowBlockContent>()
            .Which.RichCells[0].Should().Match<NotionTableCell>(cell => cell.ColSpan == 2 && cell.RowSpan == 2);
        cut.WaitForAssertion(() => cut.Find(".tm-notion-table__cell-td[colspan='2'][rowspan='2']").Should().NotBeNull());

        cut.Find("button[title='Undo table change']").Click();
        provider.Rows[1].Content.Should().BeOfType<TableRowBlockContent>()
            .Which.RichCells[0].Should().Match<NotionTableCell>(cell => cell.ColSpan == 1 && cell.RowSpan == 1 && !cell.IsMergeHidden);

        await cut.InvokeAsync(() => table.Instance.SetTableSelection(1, 0, 2, 1));
        cut.Find("button[title='Merge cells']").Click();

        await cut.InvokeAsync(() => table.Instance.SetTableSelection(1, 0, 1, 0));
        cut.Find("button[title='Yellow']").Click();
        provider.Rows[1].Content.Should().BeOfType<TableRowBlockContent>()
            .Which.RichCells[0].BackgroundColor.Should().Be("color-mix(in srgb, var(--tm-color-warning) 16%, var(--tm-bg-surface))");

        cut.Find("button[title='Split']").Click();
        provider.Rows[1].Content.Should().BeOfType<TableRowBlockContent>()
            .Which.RichCells[0].Should().Match<NotionTableCell>(cell => cell.ColSpan == 1 && cell.RowSpan == 1 && !cell.IsMergeHidden);

        cut.Find("button[title='Sort column']").Click();
        provider.OrderedRowIds.Should().NotBeNull();
        provider.OrderedRowIds!.Skip(1).Should().Equal(provider.Rows[2].Id.ToString(), provider.Rows[1].Id.ToString());
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderTable(TableBlockProvider provider)
    {
        var context = new NotionEditorContext { BlockProvider = provider };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionTableBlock>(child => child
                .Add(component => component.Block, provider.Table)
                .Add(component => component.Content, (ITableBlockContent)provider.Table.Content)));
    }

    private sealed class TableBlockProvider : INotionBlockProvider
    {
        private static readonly Guid PageId = Guid.Parse("cf110000-0000-0000-0000-000000000001");

        public PageBlock Table { get; } = new()
        {
            Id = Guid.Parse("cf110000-0000-0000-0000-000000000010"),
            PageId = PageId,
            Type = BlockType.Table,
            Order = 0,
            Content = new TableBlockContent { HasHeaderRow = true, ColumnCount = 3 }
        };

        public List<IPageBlock> Rows { get; } =
        [
            Row("cf110000-0000-0000-0000-000000000011", 0, ["Name", "Status", "Score"]),
            Row("cf110000-0000-0000-0000-000000000012", 1, ["Beta", "Open", "20"]),
            Row("cf110000-0000-0000-0000-000000000013", 2, ["Alpha", "Done", "10"])
        ];

        public IReadOnlyList<string>? OrderedRowIds { get; private set; }

        public Task<IEnumerable<IPageBlock>> GetBlocksAsync(string pageId)
            => Task.FromResult<IEnumerable<IPageBlock>>([]);

        public Task<IEnumerable<IPageBlock>> GetChildBlocksAsync(string parentBlockId)
            => Task.FromResult<IEnumerable<IPageBlock>>(Rows.OrderBy(row => row.Order));

        public Task<IPageBlock> CreateBlockAsync(string pageId, IPageBlock block, string? afterBlockId)
        {
            Rows.Add(block);
            return Task.FromResult(block);
        }

        public Task<IEnumerable<IPageBlock>> CreateBlocksAsync(string pageId, IEnumerable<IPageBlock> blocks, string? afterBlockId)
        {
            var created = blocks.ToList();
            Rows.AddRange(created);
            return Task.FromResult<IEnumerable<IPageBlock>>(created);
        }

        public Task UpdateBlockAsync(IPageBlock block)
        {
            if (block.Id == Table.Id)
            {
                Table.Content = block.Content;
                return Task.CompletedTask;
            }

            var index = Rows.FindIndex(row => row.Id == block.Id);
            if (index >= 0) Rows[index] = block;
            return Task.CompletedTask;
        }

        public Task DeleteBlockAsync(string blockId)
        {
            Rows.RemoveAll(row => row.Id.ToString() == blockId);
            return Task.CompletedTask;
        }

        public Task ReorderBlocksAsync(string pageId, IEnumerable<string> orderedBlockIds)
        {
            OrderedRowIds = orderedBlockIds.ToList();
            return Task.CompletedTask;
        }

        public Task MoveBlockAsync(MoveNotionBlockRequest request) => Task.CompletedTask;
        public Task MoveBlockToPageAsync(string blockId, string targetPageId, string? afterBlockId) => Task.CompletedTask;
        public Task<IPageBlock> DuplicateBlockAsync(string blockId) => throw new NotSupportedException();
        public Task<IPageBlock> ConvertBlockTypeAsync(string blockId, BlockType newType) => throw new NotSupportedException();
        public Task<string> GetBlockLinkAsync(string blockId) => Task.FromResult(string.Empty);

        private static PageBlock Row(string id, int order, IReadOnlyList<string> cells) => new()
        {
            Id = Guid.Parse(id),
            PageId = PageId,
            ParentBlockId = Guid.Parse("cf110000-0000-0000-0000-000000000010"),
            Type = BlockType.TableRow,
            Order = order,
            Content = new TableRowBlockContent { Cells = cells }
        };
    }
}
