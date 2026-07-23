using System.Text.Json;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal static class NotionAggregateBaselineValidator
{
    public static IReadOnlyList<NotionAggregateIssue> Validate(NotionAggregateWorkingSet workingSet)
    {
        var issues = new List<NotionAggregateIssue>();
        var globallySeenBlockIds = new HashSet<Guid>();
        var pages = workingSet.Pages.Values.OrderBy(page => page.Page.Id).ToList();

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = pages[pageIndex];
            var pagePath = $"$.pages[{pageIndex}]";
            if (page.SchemaVersion != NotionPageSnapshot.CurrentSchemaVersion)
            {
                Add(
                    "unsupported_schema_version",
                    $"Snapshot schema version {page.SchemaVersion} is not supported.",
                    $"{pagePath}.schemaVersion");
            }
            if (page.Page.Id == Guid.Empty)
            {
                Add("page_id_required", "Page id must be preallocated.", $"{pagePath}.page.id");
            }

            var byId = new Dictionary<Guid, NotionBlockSnapshot>();
            var indexById = new Dictionary<Guid, int>();
            for (var blockIndex = 0; blockIndex < page.Blocks.Count; blockIndex++)
            {
                var block = page.Blocks[blockIndex];
                var blockPath = $"{pagePath}.blocks[{blockIndex}]";
                if (block.Id == Guid.Empty)
                {
                    Add("block_id_required", "Block id must be preallocated.", $"{blockPath}.id");
                }
                else if (!globallySeenBlockIds.Add(block.Id))
                {
                    Add("duplicate_block_id", $"Block id '{block.Id}' is duplicated.", $"{blockPath}.id");
                }

                if (block.PageId != page.Page.Id)
                {
                    Add(
                        "block_page_mismatch",
                        $"Block '{block.Id}' belongs to page '{block.PageId}', not '{page.Page.Id}'.",
                        $"{blockPath}.pageId");
                }
                if (block.Order < 0)
                {
                    Add("negative_block_order", "Block order cannot be negative.", $"{blockPath}.order");
                }
                if (block.Content.ValueKind is System.Text.Json.JsonValueKind.Undefined or
                    System.Text.Json.JsonValueKind.Null)
                {
                    Add("block_content_required", "Block content is required.", $"{blockPath}.content");
                }

                if (block.Id != Guid.Empty)
                {
                    byId.TryAdd(block.Id, block);
                    indexById.TryAdd(block.Id, blockIndex);
                }
            }

            for (var blockIndex = 0; blockIndex < page.Blocks.Count; blockIndex++)
            {
                var block = page.Blocks[blockIndex];
                var blockPath = $"{pagePath}.blocks[{blockIndex}]";
                if (block.ParentBlockId is { } parentId && !byId.ContainsKey(parentId))
                {
                    Add(
                        "parent_block_not_found",
                        $"Parent block '{parentId}' was not found on page '{page.Page.Id}'.",
                        $"{blockPath}.parentBlockId");
                    continue;
                }

                var visited = new HashSet<Guid> { block.Id };
                var cursor = block.ParentBlockId;
                while (cursor is { } current && byId.TryGetValue(current, out var parent))
                {
                    if (!visited.Add(current))
                    {
                        Add(
                            "block_parent_cycle",
                            $"Block '{block.Id}' participates in a parent cycle.",
                            $"{blockPath}.parentBlockId");
                        break;
                    }

                    cursor = parent.ParentBlockId;
                }

                if (block.Type == BlockType.TableRow)
                {
                    if (block.ParentBlockId is null)
                    {
                        Add(
                            "table_row_parent_required",
                            "A tableRow block must be a direct child of a table block.",
                            $"{blockPath}.parentBlockId",
                            "Set parentBlockId to the containing table id.");
                    }
                    else if (byId.TryGetValue(block.ParentBlockId.Value, out var parent) &&
                             parent.Type != BlockType.Table)
                    {
                        Add(
                            "table_row_parent_type_invalid",
                            "A tableRow parent must have block type Table.",
                            $"{blockPath}.parentBlockId",
                            "Move the row under a Table block.");
                    }
                }

                if (block.ParentBlockId is { } tableParentId &&
                    byId.TryGetValue(tableParentId, out var tableParent) &&
                    tableParent.Type == BlockType.Table &&
                    block.Type != BlockType.TableRow)
                {
                    Add(
                        "table_child_type_invalid",
                        "A table block may contain only tableRow children.",
                        $"{blockPath}.type",
                        "Convert the child to TableRow or move it outside the table.");
                }
            }

            foreach (var siblings in page.Blocks.GroupBy(block => block.ParentBlockId))
            {
                var orders = siblings.OrderBy(block => block.Order).Select(block => block.Order).ToList();
                if (!orders.SequenceEqual(Enumerable.Range(0, orders.Count)))
                {
                    Add(
                        "non_contiguous_block_order",
                        "Sibling block order must be contiguous and zero-based.",
                        $"{pagePath}.blocks",
                        "Renumber each sibling set from zero without gaps or duplicates.");
                }
            }

            for (var blockIndex = 0; blockIndex < page.Blocks.Count; blockIndex++)
            {
                var tableBlock = page.Blocks[blockIndex];
                if (tableBlock.Type != BlockType.Table)
                {
                    continue;
                }

                var tablePath = $"{pagePath}.blocks[{blockIndex}]";
                NotionAuthoringTable? table;
                try
                {
                    table = tableBlock.Content.Deserialize<NotionAuthoringTable>(
                        NotionAggregateJson.Options);
                }
                catch (JsonException ex)
                {
                    Add(
                        "table_content_invalid",
                        ex.Message,
                        $"{tablePath}.content",
                        "Use the documented canonical table content shape.");
                    continue;
                }

                if (table is null)
                {
                    Add(
                        "table_content_invalid",
                        "Table content is required.",
                        $"{tablePath}.content",
                        "Supply canonical table content.");
                    continue;
                }

                ValidateTableMetadata(table, tablePath);
                var rowBlocks = page.Blocks
                    .Where(candidate =>
                        candidate.ParentBlockId == tableBlock.Id &&
                        candidate.Type == BlockType.TableRow)
                    .OrderBy(candidate => candidate.Order)
                    .ThenBy(candidate => candidate.Id)
                    .ToList();
                var logicalRows = new List<NotionAuthoringTableRow>(rowBlocks.Count);
                var rowPaths = new List<string>(rowBlocks.Count);
                foreach (var rowBlock in rowBlocks)
                {
                    var rowPath = $"{pagePath}.blocks[{indexById[rowBlock.Id]}]";
                    rowPaths.Add(rowPath);
                    try
                    {
                        var row = rowBlock.Content.Deserialize<NotionAuthoringTableRow>(
                            NotionAggregateJson.Options);
                        if (row is null)
                        {
                            Add(
                                "table_row_content_invalid",
                                "Table row content is required.",
                                $"{rowPath}.content",
                                "Supply canonical tableRow content.");
                        }
                        else
                        {
                            logicalRows.Add(row);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Add(
                            "table_row_content_invalid",
                            ex.Message,
                            $"{rowPath}.content",
                            "Use the documented canonical tableRow content shape.");
                    }
                }

                if (logicalRows.Count != rowBlocks.Count)
                {
                    continue;
                }

                const string projectionRoot = "$.__notionTable.rows";
                if (!NotionTableGridProjector.TryProject(
                        logicalRows,
                        table.ColumnCount,
                        projectionRoot,
                        out _,
                        out var tableIssues))
                {
                    foreach (var issue in tableIssues)
                    {
                        issues.Add(RemapTableIssue(issue, projectionRoot, rowPaths, tablePath));
                    }
                }
            }

            void ValidateTableMetadata(NotionAuthoringTable table, string tablePath)
            {
                if (table.ColumnCount < 1 ||
                    table.ColumnCount > NotionAuthoringLimits.MaxTableColumns)
                {
                    Add(
                        "table_column_limit_exceeded",
                        $"columnCount must be between 1 and {NotionAuthoringLimits.MaxTableColumns}.",
                        $"{tablePath}.content.columnCount",
                        "Use a supported positive column count.");
                }
                var alignments = table.ColumnAlignments ?? [];
                if (alignments.Count is not 0 &&
                    alignments.Count != table.ColumnCount)
                {
                    Add(
                        "table_column_alignment_count_mismatch",
                        "columnAlignments must be empty or contain exactly one value per column.",
                        $"{tablePath}.content.columnAlignments",
                        "Supply one alignment per column or an empty array.");
                }
                var widths = table.ColumnWidths ?? [];
                if (widths.Count is not 0 &&
                    widths.Count != table.ColumnCount)
                {
                    Add(
                        "table_column_width_count_mismatch",
                        "columnWidths must be empty or contain exactly one value per column.",
                        $"{tablePath}.content.columnWidths",
                        "Supply one width per column or an empty array.");
                }
                for (var columnIndex = 0; columnIndex < widths.Count; columnIndex++)
                {
                    var width = widths[columnIndex];
                    if (width is <= 0 || width is { } value && !double.IsFinite(value))
                    {
                        Add(
                            "table_width_out_of_range",
                            "Column widths must be finite positive numbers or null.",
                            $"{tablePath}.content.columnWidths[{columnIndex}]",
                            "Use a finite positive CSS-pixel width or null.");
                    }
                }
            }
        }

        return issues;

        static NotionAggregateIssue RemapTableIssue(
            NotionAggregateIssue issue,
            string projectionRoot,
            IReadOnlyList<string> rowPaths,
            string tablePath)
        {
            var path = issue.Path;
            if (path is not null && path.StartsWith(projectionRoot + "[", StringComparison.Ordinal))
            {
                var indexStart = projectionRoot.Length + 1;
                var indexEnd = path.IndexOf(']', indexStart);
                if (indexEnd > indexStart &&
                    int.TryParse(path[indexStart..indexEnd], out var rowIndex) &&
                    rowIndex >= 0 &&
                    rowIndex < rowPaths.Count)
                {
                    path = rowPaths[rowIndex] + ".content" + path[(indexEnd + 1)..];
                }
            }
            else if (path == projectionRoot)
            {
                path = $"{tablePath}.content.rows";
            }
            else if (path == "$.__notionTable.columnCount")
            {
                path = $"{tablePath}.content.columnCount";
            }

            return new NotionAggregateIssue
            {
                Code = issue.Code,
                Severity = issue.Severity,
                Message = issue.Message,
                Path = path,
                SuggestedFix = issue.SuggestedFix
            };
        }

        void Add(string code, string message, string path, string? suggestedFix = null)
            => issues.Add(new NotionAggregateIssue
            {
                Code = code,
                Severity = NotionIssueSeverity.Error,
                Message = message,
                Path = path,
                SuggestedFix = suggestedFix ??
                    $"Correct the value at {path} and retry the atomic request."
            });
    }
}
