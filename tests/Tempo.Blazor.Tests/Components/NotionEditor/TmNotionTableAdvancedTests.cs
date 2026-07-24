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
            ["TmNotionTableBlock_Redo"] = "Redo table change",
            ["TmNotionTableBlock_ConflictTitle"] = "Save conflict",
            ["TmNotionTableBlock_ConflictMessage"] = "Your changes are local.",
            ["TmNotionTableBlock_ConflictReload"] = "Reload server version",
            ["TmNotionTableBlock_ConflictReapply"] = "Reapply my changes",
            ["TmNotionTableBlock_SaveError"] = "The table could not be saved.",
            ["TmNotionTableBlock_ClearColor"] = "Clear color",
            ["TmNotionTableBlock_ColorYellow"] = "Yellow",
            ["TmNotionTableBlock_ColorGreen"] = "Green",
            ["TmNotionTableBlock_ColorBlue"] = "Blue",
            ["TmNotionTableBlock_ColorRed"] = "Red",
            ["TmNotionTableBlock_Sort"] = "Sort column"
        });
    }

    [Fact]
    public void TableRowContent_SerializesCanonicalRichCells()
    {
        var content = new TableRowBlockContent
        {
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
        restored!.RichCells.Should().ContainSingle().Which.Should().BeEquivalentTo(content.RichCells[0]);
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
    public void TableWithoutAggregateSession_RendersCanonicalCellsAsReadOnly()
    {
        var cut = RenderTable(new TableBlockService());

        cut.WaitForAssertion(() =>
            cut.FindAll(".tm-notion-table__cell-td").Should().HaveCount(9));
        cut.FindAll(".tm-notion-table__cell[contenteditable='true']").Should().BeEmpty();
        cut.FindAll(".tm-notion-table-block__add-row").Should().BeEmpty();
        cut.Find(".tm-notion-table-block")
            .GetAttribute("data-aggregate-enabled").Should().Be("false");
    }

    [Fact]
    public async Task AggregateTable_MergeUndoRedo_EachPersistsAsOnePageSave()
    {
        var provider = new AggregateTableProvider(CreateAggregateSnapshot());
        var cut = await RenderAggregateTableAsync(provider);
        var table = cut.FindComponent<TmNotionTableBlock>();

        await cut.InvokeAsync(() => table.Instance.SetTableSelection(0, 0, 1, 1));
        cut.Find("button[title='Merge cells']").Click();
        provider.SaveRequests.Should().ContainSingle();
        cut.WaitForAssertion(() =>
            cut.Find(".tm-notion-table__cell-td[colspan='2'][rowspan='2']")
                .Should().NotBeNull());

        cut.Find("button[title='Undo table change']").Click();
        provider.SaveRequests.Should().HaveCount(2);
        cut.FindAll(".tm-notion-table__cell-td").Should().HaveCount(4);

        cut.Find("button[title='Redo table change']").Click();
        provider.SaveRequests.Should().HaveCount(3);
        cut.WaitForAssertion(() =>
            cut.Find(".tm-notion-table__cell-td[colspan='2'][rowspan='2']")
                .Should().NotBeNull());
    }

    [Fact]
    public async Task AggregateTable_ConflictKeepsLocalGrid_AndOffersReloadOrReapply()
    {
        var provider = new AggregateTableProvider(CreateAggregateSnapshot())
        {
            ConflictNextSave = true
        };
        var cut = await RenderAggregateTableAsync(provider);
        var table = cut.FindComponent<TmNotionTableBlock>();

        await cut.InvokeAsync(() => table.Instance.SetTableSelection(0, 0, 1, 1));
        cut.Find("button[title='Merge cells']").Click();

        provider.SaveRequests.Should().ContainSingle();
        cut.Find("[data-testid='notion-table-conflict']")
            .TextContent.Should().Contain("Save conflict");
        cut.Find(".tm-notion-table__cell-td[colspan='2'][rowspan='2']")
            .Should().NotBeNull();

        provider.Remote.ConcurrencyToken = "remote-token";
        cut.FindAll("button").Single(button =>
            button.TextContent.Contains("Reapply my changes", StringComparison.Ordinal)).Click();

        provider.SaveRequests.Should().HaveCount(2);
        provider.SaveRequests[1].Pages[0].BaseConcurrencyToken.Should().Be("remote-token");
        cut.FindAll("[data-testid='notion-table-conflict']").Should().BeEmpty();
    }

    [Fact]
    public async Task AggregateTable_DeleteColumnThroughMergedCell_PreservesOriginAndValidGrid()
    {
        var provider = new AggregateTableProvider(CreateAggregateSnapshot());
        var cut = await RenderAggregateTableAsync(provider);
        var table = cut.FindComponent<TmNotionTableBlock>();

        await cut.InvokeAsync(() => table.Instance.SetTableSelection(0, 0, 0, 1));
        cut.Find("button[title='Merge cells']").Click();
        cut.FindAll(".tm-notion-table__col-delete").First().Click();

        provider.SaveRequests.Should().HaveCount(2);
        NotionAggregateValidator.Validate([provider.Remote]).Should().BeEmpty();
        var canonicalTable = provider.Remote.Blocks
            .Single(block => block.Type == BlockType.Table)
            .Content.Deserialize<NotionAuthoringTable>(NotionAggregateJson.Options)!;
        canonicalTable.ColumnCount.Should().Be(1);
        var firstRow = provider.Remote.Blocks
            .Where(block => block.Type == BlockType.TableRow)
            .OrderBy(block => block.Order)
            .First()
            .Content.Deserialize<NotionAuthoringTableRow>(NotionAggregateJson.Options)!;
        firstRow.Cells.Should().ContainSingle();
        firstRow.Cells[0].Html.Should().Be("A");
        firstRow.Cells[0].ColumnSpan.Should().Be(1);
    }

    [Fact]
    public void TableRow_RendersCanonicalStylesMarksAndSafeLinks()
    {
        var row = new PageBlock
        {
            Id = Guid.NewGuid(),
            PageId = Guid.NewGuid(),
            Type = BlockType.TableRow,
            Content = new TableRowBlockContent
            {
                RichCells =
                [
                    new NotionTableCell
                    {
                        Html = "<strong>Safe</strong><script>bad()</script>",
                        BackgroundColor = "#ffeeaa",
                        TextColor = "#123456",
                        HorizontalAlignment = NotionTableHorizontalAlignment.Right,
                        VerticalAlignment = NotionTableVerticalAlignment.Middle,
                        Width = 180,
                        Borders = new NotionTableCellBorders
                        {
                            Bottom = new NotionTableBorder
                            {
                                Style = NotionTableBorderStyle.Dashed,
                                Color = "#abcdef",
                                Width = 2
                            }
                        }
                    }
                ]
            }
        };

        var cut = Render<TmNotionTableRowBlock>(parameters => parameters
            .Add(component => component.Row, row)
            .Add(component => component.ColumnCount, 1)
            .Add(component => component.ReadOnly, true));

        var cell = cut.Find(".tm-notion-table__cell-td");
        cell.ClassList.Should().Contain("tm-notion-table__cell-td--align-right");
        cell.GetAttribute("style").Should().Contain("--tm-notion-table-cell-background:#ffeeaa");
        cell.GetAttribute("style").Should().Contain("--tm-notion-table-cell-text:#123456");
        cell.GetAttribute("style").Should().Contain("--tm-notion-table-cell-width:180px");
        cell.GetAttribute("style").Should().Contain("--tm-notion-table-cell-vertical:middle");
        cell.GetAttribute("style").Should().Contain(
            "--tm-notion-table-cell-border-bottom:2px dashed #abcdef");
        cell.InnerHtml.Should().Contain("<strong>Safe</strong>");
        cell.InnerHtml.Should().NotContain("<script");
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderTable(TableBlockService provider)
    {
        var context = new NotionEditorContext { BlockService = provider };

        return Render<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionTableBlock>(child => child
                .Add(component => component.Block, provider.Table)
                .Add(component => component.Content, (ITableBlockContent)provider.Table.Content)));
    }

    private async Task<IRenderedComponent<CascadingValue<NotionEditorContext>>> RenderAggregateTableAsync(
        AggregateTableProvider provider)
    {
        var session = new NotionEditorAggregateSession(provider);
        (await session.LoadAsync(provider.PageId)).Success.Should().BeTrue();
        var view = NotionCanonicalTableBridge.ToView(session.CurrentSnapshot!, provider.TableId);
        var context = new NotionEditorContext
        {
            BlockService = new TableBlockService(),
            AggregateSession = session
        };

        return Render<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionTableBlock>(child => child
                .Add(component => component.Block, view.Table)
                .Add(component => component.Content, (ITableBlockContent)view.Table.Content)));
    }

    private static NotionPageSnapshot CreateAggregateSnapshot()
    {
        var pageId = Guid.Parse("cf120000-0000-0000-0000-000000000001");
        var tableId = Guid.Parse("cf120000-0000-0000-0000-000000000010");
        return new NotionPageSnapshot
        {
            Page = new NotionPageState { Id = pageId, Title = "Atomic table" },
            ConcurrencyToken = "token-1",
            Digest = "digest-1",
            Blocks =
            [
                AggregateBlock(
                    tableId,
                    pageId,
                    null,
                    BlockType.Table,
                    0,
                    new NotionAuthoringTable
                    {
                        ColumnCount = 2,
                        ColumnWidths = [160, 180]
                    }),
                AggregateBlock(
                    Guid.Parse("cf120000-0000-0000-0000-000000000011"),
                    pageId,
                    tableId,
                    BlockType.TableRow,
                    0,
                    new NotionAuthoringTableRow
                    {
                        Cells =
                        [
                            new NotionAuthoringTableCell { Html = "A" },
                            new NotionAuthoringTableCell { Html = "B" }
                        ]
                    }),
                AggregateBlock(
                    Guid.Parse("cf120000-0000-0000-0000-000000000012"),
                    pageId,
                    tableId,
                    BlockType.TableRow,
                    1,
                    new NotionAuthoringTableRow
                    {
                        Cells =
                        [
                            new NotionAuthoringTableCell { Html = "C" },
                            new NotionAuthoringTableCell { Html = "D" }
                        ]
                    })
            ]
        };
    }

    private static NotionBlockSnapshot AggregateBlock<T>(
        Guid id,
        Guid pageId,
        Guid? parentId,
        BlockType type,
        int order,
        T content)
        => new()
        {
            Id = id,
            PageId = pageId,
            ParentBlockId = parentId,
            Type = type,
            Order = order,
            Content = JsonSerializer.SerializeToElement(content, NotionAggregateJson.Options)
        };

    private sealed class TableBlockService : INotionEditorBlockService
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
            Content = new TableRowBlockContent
            {
                RichCells = cells.Select(cell => new NotionTableCell { Html = cell }).ToList()
            }
        };
    }

    private sealed class AggregateTableProvider : INotionAggregateProvider
    {
        public AggregateTableProvider(NotionPageSnapshot initial)
        {
            Remote = Clone(initial);
            PageId = initial.Page.Id;
            TableId = initial.Blocks.Single(block => block.Type == BlockType.Table).Id;
        }

        public Guid PageId { get; }
        public Guid TableId { get; }
        public NotionPageSnapshot Remote { get; set; }
        public bool ConflictNextSave { get; set; }
        public List<NotionAggregateSaveRequest> SaveRequests { get; } = [];

        public Task<NotionAggregateLoadResult> LoadPageAsync(
            Guid pageId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new NotionAggregateLoadResult
            {
                Found = pageId == PageId,
                Snapshot = Clone(Remote)
            });

        public Task<NotionAggregateLoadResult> LoadBlockAsync(
            Guid blockId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new NotionAggregateLoadResult { Found = false });

        public Task<NotionAggregateSaveResult> SaveAsync(
            NotionAggregateSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            if (ConflictNextSave)
            {
                ConflictNextSave = false;
                return Task.FromResult(new NotionAggregateSaveResult
                {
                    Conflict = true,
                    Conflicts =
                    [
                        new NotionPageConflict
                        {
                            PageId = PageId,
                            ExpectedConcurrencyToken = request.Pages[0].BaseConcurrencyToken,
                            CurrentConcurrencyToken = Remote.ConcurrencyToken
                        }
                    ]
                });
            }

            Remote = Clone(request.Pages[0].Snapshot);
            Remote.ConcurrencyToken = $"token-{SaveRequests.Count + 1}";
            Remote.Digest = $"digest-{SaveRequests.Count + 1}";
            return Task.FromResult(new NotionAggregateSaveResult
            {
                Success = true,
                Pages =
                [
                    new NotionSavedPage
                    {
                        PageId = PageId,
                        ConcurrencyToken = Remote.ConcurrencyToken,
                        Digest = Remote.Digest
                    }
                ]
            });
        }

        private static NotionPageSnapshot Clone(NotionPageSnapshot snapshot)
            => JsonSerializer.Deserialize<NotionPageSnapshot>(
                JsonSerializer.Serialize(snapshot, NotionAggregateJson.Options),
                NotionAggregateJson.Options)!;
    }
}
