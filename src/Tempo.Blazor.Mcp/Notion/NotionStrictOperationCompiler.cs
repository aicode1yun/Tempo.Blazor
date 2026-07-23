using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Notion;

internal sealed class NotionStrictOperationCompiler : INotionAtomicOperationCompiler
{
    private static readonly HashSet<string> BlockFields =
        ["type", "content", "children"];
    private static readonly HashSet<string> TableRowFields = ["cells"];
    private static readonly HashSet<string> TableCellFields =
    [
        "html",
        "inlines",
        "backgroundColor",
        "textColor",
        "horizontalAlignment",
        "verticalAlignment",
        "rowSpan",
        "columnSpan",
        "width",
        "borders"
    ];
    private static readonly HashSet<string> InlineFields =
    [
        "text",
        "href",
        "bold",
        "italic",
        "underline",
        "strikethrough",
        "code",
        "textColor",
        "backgroundColor"
    ];
    private static readonly HashSet<string> BorderContainerFields =
        ["top", "right", "bottom", "left"];
    private static readonly HashSet<string> BorderFields =
        ["style", "color", "width"];
    private static readonly HashSet<string> CreateBlockFields =
        ["op", "clientRef", "pageId", "parentBlockId", "order", "block"];
    private static readonly HashSet<string> CreateBlocksFields =
        ["op", "clientRef", "pageId", "parentBlockId", "order", "blocks"];
    private static readonly HashSet<string> CreateTableFields =
    [
        "op",
        "clientRef",
        "pageId",
        "parentBlockId",
        "order",
        "columnCount",
        "hasHeaderRow",
        "hasHeaderColumn",
        "columnAlignments",
        "columnWidths",
        "rows"
    ];
    private static readonly HashSet<string> PatchFields =
        ["op", "clientRef", "blockId", "patch"];
    private static readonly HashSet<string> MoveFields =
    [
        "op",
        "clientRef",
        "blockId",
        "targetPageId",
        "targetParentBlockId",
        "targetOrder"
    ];
    private static readonly HashSet<string> ReorderFields =
        ["op", "clientRef", "pageId", "parentBlockId", "orderedBlockIds"];
    private static readonly HashSet<string> ConvertFields =
        ["op", "clientRef", "blockId", "newType", "content"];
    private static readonly HashSet<string> DeleteFields =
        ["op", "clientRef", "blockId"];
    private static readonly HashSet<string> ReplaceFields =
        ["op", "clientRef", "pageId", "parentBlockId", "blocks"];

    public NotionOperationTargetDiscoveryResult DiscoverTargets(JsonArray source)
    {
        var targets = new HashSet<NotionAggregateTarget>();
        var issues = new List<NotionAggregateIssue>();
        for (var index = 0; index < source.Count; index++)
        {
            var path = $"$.operations[{index}]";
            if (source[index] is not JsonObject operation)
            {
                issues.Add(Error(
                    "operation_must_be_object",
                    "Each operation must be a JSON object.",
                    path));
                continue;
            }
            if (operation["op"] is not JsonValue opValue ||
                !opValue.TryGetValue<string>(out var op) ||
                string.IsNullOrWhiteSpace(op))
            {
                issues.Add(operation.ContainsKey("type")
                    ? UnknownField($"{path}.type")
                    : Error(
                        "operation_discriminator_required",
                        "A string 'op' discriminator is required.",
                        $"{path}.op"));
                continue;
            }

            switch (op)
            {
                case "createBlock":
                case "createBlocks":
                case "createTable":
                case "reorderBlocks":
                case "replaceBlocks":
                    AddPageTarget(operation, "pageId", path);
                    break;
                case "patchBlockContent":
                case "convertBlockType":
                case "deleteBlock":
                    AddBlockTarget(operation, "blockId", path);
                    break;
                case "moveBlock":
                    AddBlockTarget(operation, "blockId", path);
                    AddPageTarget(operation, "targetPageId", path);
                    break;
                default:
                    issues.Add(Error(
                        "unknown_operation",
                        $"Unknown operation '{op}'.",
                        $"{path}.op"));
                    break;
            }
        }

        return new NotionOperationTargetDiscoveryResult
        {
            Targets = targets
                .OrderBy(target => target.Kind)
                .ThenBy(target => target.Id)
                .ToList(),
            Issues = issues
        };

        void AddPageTarget(JsonObject operation, string name, string path)
        {
            var id = ReadGuid(operation, name, path, issues);
            if (id is not null)
            {
                targets.Add(new NotionAggregateTarget(NotionAggregateTargetKind.Page, id.Value));
            }
        }

        void AddBlockTarget(JsonObject operation, string name, string path)
        {
            var id = ReadGuid(operation, name, path, issues);
            if (id is not null)
            {
                targets.Add(new NotionAggregateTarget(NotionAggregateTargetKind.Block, id.Value));
            }
        }
    }

    public ValueTask<NotionOperationCompilationResult> CompileAsync(
        JsonArray source,
        NotionAggregateWorkingSet workingSet,
        NotionOperationCompileContext context,
        CancellationToken cancellationToken)
    {
        var operations = new List<NotionCanonicalOperation>();
        var issues = new List<NotionAggregateIssue>();
        var preview = new NotionAggregateWorkingSet(workingSet.Pages);

        for (var index = 0; index < source.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = $"$.operations[{index}]";
            if (source[index] is not JsonObject operation)
            {
                issues.Add(Error(
                    "operation_must_be_object",
                    "Each operation must be a JSON object.",
                    path));
                continue;
            }

            if (operation["op"] is not JsonValue opValue ||
                !opValue.TryGetValue<string>(out var op) ||
                string.IsNullOrWhiteSpace(op))
            {
                if (operation.ContainsKey("type"))
                {
                    issues.Add(UnknownField($"{path}.type"));
                }
                else
                {
                    issues.Add(Error(
                        "operation_discriminator_required",
                        "A string 'op' discriminator is required.",
                        $"{path}.op"));
                }
                continue;
            }

            var clientRef = ReadOptionalString(operation, "clientRef", path, issues);
            if (clientRef is not null && string.IsNullOrWhiteSpace(clientRef))
            {
                issues.Add(Error(
                    "client_ref_invalid",
                    "clientRef must be a non-empty string when supplied.",
                    $"{path}.clientRef"));
            }
            var issueCount = issues.Count;
            switch (op)
            {
                case "createBlock":
                    CompileCreateBlock(operation, index, path, clientRef);
                    break;
                case "createBlocks":
                    CompileCreateBlocks(operation, index, path, clientRef);
                    break;
                case "createTable":
                    CompileCreateTable(operation, index, path, clientRef);
                    break;
                case "patchBlockContent":
                    CompilePatch(operation, index, path, clientRef);
                    break;
                case "moveBlock":
                    CompileMove(operation, index, path, clientRef);
                    break;
                case "reorderBlocks":
                    CompileReorder(operation, index, path, clientRef);
                    break;
                case "convertBlockType":
                    CompileConvert(operation, index, path, clientRef);
                    break;
                case "deleteBlock":
                    CompileDelete(operation, index, path, clientRef);
                    break;
                case "replaceBlocks":
                    CompileReplace(operation, index, path, clientRef);
                    break;
                default:
                    issues.Add(Error(
                        "unknown_operation",
                        $"Unknown operation '{op}'.",
                        $"{path}.op"));
                    break;
            }

            if (issues.Count > issueCount)
            {
                continue;
            }
        }

        return ValueTask.FromResult(
            issues.Count == 0
                ? NotionOperationCompilationResult.Compiled(operations)
                : NotionOperationCompilationResult.Failed(issues.ToArray()));

        void CompileCreateBlock(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    CreateBlockFields,
                    path,
                    issues))
            {
                return;
            }

            var pageId = ReadGuid(operation, "pageId", path, issues);
            var parentBlockId = ReadOptionalGuid(operation, "parentBlockId", path, issues);
            var order = ReadOptionalOrder(operation, "order", path, issues);
            if (operation["block"] is not JsonObject block)
            {
                issues.Add(Error(
                    "block_required",
                    "createBlock requires a block object.",
                    $"{path}.block"));
                return;
            }
            if (pageId is null || issues.Count > 0)
            {
                return;
            }

            var resolvedOrder = order ?? ReserveNextOrder(pageId.Value, parentBlockId);
            CompileBlockTree(
                block,
                operationIndex,
                clientRef,
                pageId.Value,
                parentBlockId,
                resolvedOrder,
                $"{path}.block",
                $"{operationIndex}/block");
        }

        void CompileCreateBlocks(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    CreateBlocksFields,
                    path,
                    issues))
            {
                return;
            }

            var pageId = ReadGuid(operation, "pageId", path, issues);
            var parentBlockId = ReadOptionalGuid(operation, "parentBlockId", path, issues);
            var order = ReadOptionalOrder(operation, "order", path, issues);
            if (operation["blocks"] is not JsonArray blocks)
            {
                issues.Add(Error(
                    "blocks_required",
                    "createBlocks requires a blocks array.",
                    $"{path}.blocks"));
                return;
            }
            if (blocks.Count == 0)
            {
                issues.Add(Error(
                    "blocks_required",
                    "createBlocks requires at least one block.",
                    $"{path}.blocks"));
                return;
            }
            if (pageId is null || issues.Count > 0)
            {
                return;
            }

            var nextOrder = order ?? ReserveNextOrder(pageId.Value, parentBlockId);
            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                if (blocks[blockIndex] is not JsonObject block)
                {
                    issues.Add(Error(
                        "block_must_be_object",
                        "Each blocks item must be an object.",
                        $"{path}.blocks[{blockIndex}]"));
                    continue;
                }

                CompileBlockTree(
                    block,
                    operationIndex,
                    clientRef,
                    pageId.Value,
                    parentBlockId,
                    nextOrder + blockIndex,
                    $"{path}.blocks[{blockIndex}]",
                    $"{operationIndex}/blocks/{blockIndex}");
            }
        }

        void CompileCreateTable(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    CreateTableFields,
                    path,
                    issues))
            {
                return;
            }

            var pageId = ReadGuid(operation, "pageId", path, issues);
            var parentBlockId = ReadOptionalGuid(operation, "parentBlockId", path, issues);
            var order = ReadOptionalOrder(operation, "order", path, issues);
            var columnCount = ReadRequiredInt(operation, "columnCount", path, issues, minimum: 1);
            if (operation["rows"] is not JsonArray rows)
            {
                issues.Add(Error(
                    "table_rows_required",
                    "createTable requires a rows array.",
                    $"{path}.rows"));
                return;
            }
            if (pageId is null || columnCount is null || issues.Count > 0)
            {
                return;
            }

            var table = new NotionAuthoringTable
            {
                ColumnCount = columnCount.Value,
                HasHeaderRow = ReadOptionalBoolean(operation, "hasHeaderRow", path, issues),
                HasHeaderColumn = ReadOptionalBoolean(operation, "hasHeaderColumn", path, issues),
                ColumnAlignments = DeserializeOptionalList<NotionTableHorizontalAlignment>(
                    operation,
                    "columnAlignments",
                    path,
                    issues),
                ColumnWidths = DeserializeOptionalList<double?>(
                    operation,
                    "columnWidths",
                    path,
                    issues)
            };
            if (table.ColumnAlignments.Count is not 0 &&
                table.ColumnAlignments.Count != columnCount.Value)
            {
                issues.Add(Error(
                    "table_column_metadata_mismatch",
                    "columnAlignments must be empty or contain one value per column.",
                    $"{path}.columnAlignments"));
            }
            if (table.ColumnWidths.Count is not 0 &&
                table.ColumnWidths.Count != columnCount.Value)
            {
                issues.Add(Error(
                    "table_column_metadata_mismatch",
                    "columnWidths must be empty or contain one value per column.",
                    $"{path}.columnWidths"));
            }
            for (var columnIndex = 0; columnIndex < table.ColumnWidths.Count; columnIndex++)
            {
                var width = table.ColumnWidths[columnIndex];
                if (width is <= 0 || width is { } value && !double.IsFinite(value))
                {
                    issues.Add(Error(
                        "table_width_out_of_range",
                        "Column widths must be finite positive numbers or null.",
                        $"{path}.columnWidths[{columnIndex}]"));
                }
            }

            var parsedRows = ParseTableRows(
                rows,
                columnCount.Value,
                path,
                issues,
                cancellationToken);
            if (issues.Count > 0)
            {
                return;
            }

            var tableId = DeterministicId(context, $"{operationIndex}/table");
            AddCanonical(new NotionUpsertBlockOperation(
                operationIndex,
                clientRef,
                new NotionBlockSnapshot
                {
                    Id = tableId,
                    PageId = pageId.Value,
                    ParentBlockId = parentBlockId,
                    Type = BlockType.Table,
                    Order = order ?? ReserveNextOrder(pageId.Value, parentBlockId),
                    Content = JsonSerializer.SerializeToElement(table, NotionAggregateJson.Options)
                }));
            for (var rowIndex = 0; rowIndex < parsedRows.Count; rowIndex++)
            {
                AddCanonical(new NotionUpsertBlockOperation(
                    operationIndex,
                    clientRef,
                    new NotionBlockSnapshot
                    {
                        Id = DeterministicId(context, $"{operationIndex}/table/rows/{rowIndex}"),
                        PageId = pageId.Value,
                        ParentBlockId = tableId,
                        Type = BlockType.TableRow,
                        Order = rowIndex,
                        Content = JsonSerializer.SerializeToElement(
                            parsedRows[rowIndex],
                            NotionAggregateJson.Options)
                    }));
            }
        }

        void CompilePatch(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    PatchFields,
                    path,
                    issues))
            {
                return;
            }

            var blockId = ReadGuid(operation, "blockId", path, issues);
            if (operation["patch"] is not JsonObject patch)
            {
                issues.Add(Error(
                    "content_patch_required",
                    "patchBlockContent requires a patch object.",
                    $"{path}.patch"));
            }
            if (blockId is not null && operation["patch"] is JsonObject validPatch)
            {
                AddCanonical(new NotionPatchBlockContentOperation(
                    operationIndex,
                    clientRef,
                    blockId.Value,
                    (JsonObject)validPatch.DeepClone()));
            }
        }

        void CompileMove(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    MoveFields,
                    path,
                    issues))
            {
                return;
            }

            var blockId = ReadGuid(operation, "blockId", path, issues);
            var targetPageId = ReadGuid(operation, "targetPageId", path, issues);
            var targetParentId = ReadOptionalGuid(operation, "targetParentBlockId", path, issues);
            var targetOrder = ReadRequiredInt(
                operation,
                "targetOrder",
                path,
                issues,
                minimum: 0);
            if (blockId is null || targetPageId is null || targetOrder is null)
            {
                return;
            }
            if (!preview.TryGetBlock(blockId.Value, out var sourcePageId, out _))
            {
                issues.Add(Error(
                    "block_not_found",
                    $"Block '{blockId}' was not found.",
                    $"{path}.blockId"));
                return;
            }

            AddCanonical(new NotionMoveBlockOperation(
                operationIndex,
                clientRef,
                blockId.Value,
                sourcePageId,
                targetPageId.Value,
                targetParentId,
                targetOrder.Value));
        }

        void CompileReorder(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    ReorderFields,
                    path,
                    issues))
            {
                return;
            }

            var pageId = ReadGuid(operation, "pageId", path, issues);
            var parentBlockId = ReadOptionalGuid(operation, "parentBlockId", path, issues);
            var orderedIds = ReadGuidArray(operation, "orderedBlockIds", path, issues);
            if (pageId is not null && orderedIds is not null)
            {
                AddCanonical(new NotionReorderBlocksOperation(
                    operationIndex,
                    clientRef,
                    pageId.Value,
                    parentBlockId,
                    orderedIds));
            }
        }

        void CompileConvert(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    ConvertFields,
                    path,
                    issues))
            {
                return;
            }

            var blockId = ReadGuid(operation, "blockId", path, issues);
            var newType = ReadBlockType(operation, "newType", path, issues);
            var content = ReadContent(operation, "content", path, issues);
            if (blockId is not null && newType is not null && content is not null)
            {
                AddCanonical(new NotionConvertBlockOperation(
                    operationIndex,
                    clientRef,
                    blockId.Value,
                    newType.Value,
                    content.Value));
            }
        }

        void CompileDelete(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    DeleteFields,
                    path,
                    issues))
            {
                return;
            }

            var blockId = ReadGuid(operation, "blockId", path, issues);
            if (blockId is not null)
            {
                AddCanonical(new NotionDeleteBlockOperation(
                    operationIndex,
                    clientRef,
                    blockId.Value));
            }
        }

        void CompileReplace(
            JsonObject operation,
            int operationIndex,
            string path,
            string? clientRef)
        {
            if (!ValidateAllowed(
                    operation,
                    ReplaceFields,
                    path,
                    issues))
            {
                return;
            }

            var pageId = ReadGuid(operation, "pageId", path, issues);
            var parentBlockId = ReadOptionalGuid(operation, "parentBlockId", path, issues);
            if (operation["blocks"] is not JsonArray blocks)
            {
                issues.Add(Error(
                    "blocks_required",
                    "replaceBlocks requires a blocks array.",
                    $"{path}.blocks"));
                return;
            }
            if (pageId is null)
            {
                return;
            }

            foreach (var existingId in preview.GetSiblingBlockIds(pageId.Value, parentBlockId))
            {
                AddCanonical(new NotionDeleteBlockOperation(
                    operationIndex,
                    clientRef,
                    existingId));
            }
            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                if (blocks[blockIndex] is not JsonObject block)
                {
                    issues.Add(Error(
                        "block_must_be_object",
                        "Each blocks item must be an object.",
                        $"{path}.blocks[{blockIndex}]"));
                    continue;
                }

                CompileBlockTree(
                    block,
                    operationIndex,
                    clientRef,
                    pageId.Value,
                    parentBlockId,
                    blockIndex,
                    $"{path}.blocks[{blockIndex}]",
                    $"{operationIndex}/replacement/{blockIndex}");
            }
        }

        void CompileBlockTree(
            JsonObject block,
            int operationIndex,
            string? clientRef,
            Guid pageId,
            Guid? parentBlockId,
            int order,
            string path,
            string idPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidateAllowed(block, BlockFields, path, issues))
            {
                return;
            }

            var type = ReadBlockType(block, "type", path, issues);
            var content = ReadContent(block, "content", path, issues);
            if (type is null || content is null)
            {
                return;
            }

            var blockId = DeterministicId(context, idPath);
            AddCanonical(new NotionUpsertBlockOperation(
                operationIndex,
                clientRef,
                new NotionBlockSnapshot
                {
                    Id = blockId,
                    PageId = pageId,
                    ParentBlockId = parentBlockId,
                    Type = type.Value,
                    Order = order,
                    Content = content.Value
                }));

            if (!block.TryGetPropertyValue("children", out var childrenNode))
            {
                return;
            }
            if (childrenNode is not JsonArray children)
            {
                issues.Add(Error(
                    "children_must_be_array",
                    "children must be a JSON array.",
                    $"{path}.children"));
                return;
            }

            for (var childIndex = 0; childIndex < children.Count; childIndex++)
            {
                if (children[childIndex] is not JsonObject child)
                {
                    issues.Add(Error(
                        "block_must_be_object",
                        "Each child must be a block object.",
                        $"{path}.children[{childIndex}]"));
                    continue;
                }

                CompileBlockTree(
                    child,
                    operationIndex,
                    clientRef,
                    pageId,
                    blockId,
                    childIndex,
                    $"{path}.children[{childIndex}]",
                    $"{idPath}/children/{childIndex}");
            }
        }

        int ReserveNextOrder(Guid pageId, Guid? parentBlockId)
            => preview.GetNextSiblingOrder(pageId, parentBlockId);

        void AddCanonical(NotionCanonicalOperation operation)
        {
            var apply = operation.Apply(preview);
            if (!apply.Success ||
                apply.Issues.Any(issue => issue.Severity == NotionIssueSeverity.Error))
            {
                issues.AddRange(apply.Issues.Where(
                    issue => issue.Severity == NotionIssueSeverity.Error));
                if (apply.Issues.Count == 0)
                {
                    issues.Add(Error(
                        "operation_preview_failed",
                        "The operation could not be applied to the compilation preview.",
                        $"$.operations[{operation.OperationIndex}]"));
                }
                return;
            }

            operations.Add(operation);
        }
    }

    private static IReadOnlyList<NotionAuthoringTableRow> ParseTableRows(
        JsonArray rows,
        int columnCount,
        string operationPath,
        List<NotionAggregateIssue> issues,
        CancellationToken cancellationToken)
    {
        var parsed = new List<NotionAuthoringTableRow>();
        var occupied = new int[columnCount];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowPath = $"{operationPath}.rows[{rowIndex}]";
            if (rows[rowIndex] is not JsonObject row)
            {
                issues.Add(Error("table_row_must_be_object", "Each row must be an object.", rowPath));
                continue;
            }
            if (!ValidateAllowed(row, TableRowFields, rowPath, issues))
            {
                continue;
            }
            if (row["cells"] is not JsonArray cells)
            {
                issues.Add(Error(
                    "table_cells_required",
                    "Each table row requires a cells array.",
                    $"{rowPath}.cells"));
                continue;
            }

            var logicalCells = new List<NotionAuthoringTableCell>();
            var newlyOccupied = new HashSet<int>();
            var covered = occupied.Select(value => value > 0).ToArray();
            var rowIssueCount = issues.Count;
            var column = 0;
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (column < columnCount && occupied[column] > 0)
                {
                    column++;
                }

                var cellPath = $"{rowPath}.cells[{cellIndex}]";
                if (cells[cellIndex] is not JsonObject cellNode)
                {
                    issues.Add(Error(
                        "table_cell_must_be_object",
                        "Each table cell must be an object.",
                        cellPath));
                    continue;
                }
                ValidateTableCell(cellNode, cellPath, issues);

                NotionAuthoringTableCell? cell;
                try
                {
                    cell = cellNode.Deserialize<NotionAuthoringTableCell>(
                        NotionAggregateJson.Options);
                }
                catch (JsonException ex)
                {
                    issues.Add(Error("table_cell_invalid", ex.Message, cellPath));
                    continue;
                }
                if (cell is null)
                {
                    issues.Add(Error("table_cell_invalid", "The table cell is empty.", cellPath));
                    continue;
                }
                if (cell.RowSpan < 1)
                {
                    issues.Add(Error(
                        "table_span_out_of_range",
                        "rowSpan must be at least 1.",
                        $"{cellPath}.rowSpan"));
                }
                else if (rowIndex + cell.RowSpan > rows.Count)
                {
                    issues.Add(Error(
                        "table_span_out_of_range",
                        "rowSpan exceeds the available table rows.",
                        $"{cellPath}.rowSpan"));
                }
                if (cell.ColumnSpan < 1)
                {
                    issues.Add(Error(
                        "table_span_out_of_range",
                        "columnSpan must be at least 1.",
                        $"{cellPath}.columnSpan"));
                }
                if (cell.Width is <= 0 || cell.Width is { } width && !double.IsFinite(width))
                {
                    issues.Add(Error(
                        "table_width_out_of_range",
                        "Cell width must be a finite positive number or null.",
                        $"{cellPath}.width"));
                }

                if (cell.ColumnSpan >= 1 &&
                    (column + cell.ColumnSpan > columnCount ||
                     Enumerable.Range(column, Math.Min(cell.ColumnSpan, columnCount - column))
                         .Any(index => occupied[index] > 0)))
                {
                    issues.Add(Error(
                        "table_span_out_of_range",
                        "The cell span exceeds or overlaps the logical table grid.",
                        $"{cellPath}.columnSpan"));
                }
                else if (cell.RowSpan >= 1)
                {
                    for (var offset = 0; offset < cell.ColumnSpan; offset++)
                    {
                        covered[column + offset] = true;
                        if (cell.RowSpan > 1)
                        {
                            occupied[column + offset] = cell.RowSpan - 1;
                            newlyOccupied.Add(column + offset);
                        }
                    }
                }

                column += Math.Max(1, cell.ColumnSpan);
                logicalCells.Add(cell);
            }

            if (covered.Any(value => !value) && issues.Count == rowIssueCount)
            {
                issues.Add(Error(
                    "table_row_width_mismatch",
                    "Cells and active row spans must cover every logical table column.",
                    $"{rowPath}.cells"));
            }

            for (var columnIndex = 0; columnIndex < occupied.Length; columnIndex++)
            {
                if (occupied[columnIndex] > 0 && !newlyOccupied.Contains(columnIndex))
                {
                    occupied[columnIndex]--;
                }
            }

            parsed.Add(new NotionAuthoringTableRow { Cells = logicalCells });
        }

        return parsed;
    }

    private static void ValidateTableCell(
        JsonObject cell,
        string path,
        List<NotionAggregateIssue> issues)
    {
        ValidateAllowed(cell, TableCellFields, path, issues);
        if (cell["inlines"] is JsonArray inlines)
        {
            for (var index = 0; index < inlines.Count; index++)
            {
                if (inlines[index] is JsonObject inline)
                {
                    ValidateAllowed(inline, InlineFields, $"{path}.inlines[{index}]", issues);
                }
                else
                {
                    issues.Add(Error(
                        "table_inline_must_be_object",
                        "Each inline must be an object.",
                        $"{path}.inlines[{index}]"));
                }
            }
        }
        else if (cell.ContainsKey("inlines"))
        {
            issues.Add(Error(
                "table_inlines_must_be_array",
                "inlines must be an array.",
                $"{path}.inlines"));
        }

        if (cell["borders"] is JsonObject borders)
        {
            ValidateAllowed(borders, BorderContainerFields, $"{path}.borders", issues);
            foreach (var side in BorderContainerFields)
            {
                if (borders[side] is JsonObject border)
                {
                    ValidateAllowed(border, BorderFields, $"{path}.borders.{side}", issues);
                    if (border.TryGetPropertyValue("width", out var widthNode) &&
                        (widthNode is not JsonValue widthValue ||
                         !widthValue.TryGetValue<double>(out var width) ||
                         !double.IsFinite(width) ||
                         width <= 0))
                    {
                        issues.Add(Error(
                            "table_border_width_out_of_range",
                            "Border width must be a finite positive number.",
                            $"{path}.borders.{side}.width"));
                    }
                }
                else if (borders.ContainsKey(side) && borders[side] is not null)
                {
                    issues.Add(Error(
                        "table_border_must_be_object",
                        "Each border must be an object or null.",
                        $"{path}.borders.{side}"));
                }
            }
        }
        else if (cell.ContainsKey("borders") && cell["borders"] is not null)
        {
            issues.Add(Error(
                "table_borders_must_be_object",
                "borders must be an object.",
                $"{path}.borders"));
        }
    }

    private static bool ValidateAllowed(
        JsonObject source,
        IReadOnlySet<string> allowed,
        string path,
        List<NotionAggregateIssue> issues)
    {
        var valid = true;
        foreach (var property in source)
        {
            if (!allowed.Contains(property.Key))
            {
                issues.Add(UnknownField($"{path}.{property.Key}"));
                valid = false;
            }
        }

        return valid;
    }

    private static string? ReadOptionalString(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (!source.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }
        if (node is JsonValue value && value.TryGetValue<string>(out var result))
        {
            return result;
        }

        issues.Add(Error("string_required", $"{name} must be a string.", $"{path}.{name}"));
        return null;
    }

    private static Guid? ReadGuid(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (source[name] is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text) ||
            !Guid.TryParse(text, out var id) ||
            id == Guid.Empty)
        {
            issues.Add(Error(
                "guid_required",
                $"{name} must be a non-empty GUID string.",
                $"{path}.{name}"));
            return null;
        }

        return id;
    }

    private static Guid? ReadOptionalGuid(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (!source.TryGetPropertyValue(name, out var node) || node is null)
        {
            return null;
        }

        return ReadGuid(source, name, path, issues);
    }

    private static int? ReadOptionalOrder(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (!source.ContainsKey(name))
        {
            return null;
        }

        return ReadRequiredInt(source, name, path, issues, minimum: 0);
    }

    private static int? ReadRequiredInt(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues,
        int minimum)
    {
        if (source[name] is JsonValue value &&
            value.TryGetValue<int>(out var result) &&
            result >= minimum)
        {
            return result;
        }

        issues.Add(Error(
            "integer_out_of_range",
            $"{name} must be an integer greater than or equal to {minimum}.",
            $"{path}.{name}"));
        return null;
    }

    private static bool ReadOptionalBoolean(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (!source.ContainsKey(name))
        {
            return false;
        }
        if (source[name] is JsonValue value && value.TryGetValue<bool>(out var result))
        {
            return result;
        }

        issues.Add(Error("boolean_required", $"{name} must be a boolean.", $"{path}.{name}"));
        return false;
    }

    private static IReadOnlyList<T> DeserializeOptionalList<T>(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (!source.ContainsKey(name))
        {
            return [];
        }
        if (source[name] is not JsonArray array)
        {
            issues.Add(Error("array_required", $"{name} must be an array.", $"{path}.{name}"));
            return [];
        }

        try
        {
            return array.Deserialize<List<T>>(NotionAggregateJson.Options) ?? [];
        }
        catch (JsonException ex)
        {
            issues.Add(Error("array_invalid", ex.Message, $"{path}.{name}"));
            return [];
        }
    }

    private static BlockType? ReadBlockType(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        var value = ReadOptionalString(source, name, path, issues);
        if (value is null)
        {
            if (!source.ContainsKey(name))
            {
                issues.Add(Error(
                    "block_type_required",
                    $"{name} is required.",
                    $"{path}.{name}"));
            }
            return null;
        }

        foreach (var type in Enum.GetValues<BlockType>())
        {
            if (string.Equals(
                    JsonNamingPolicy.CamelCase.ConvertName(type.ToString()),
                    value,
                    StringComparison.Ordinal))
            {
                return type;
            }
        }

        issues.Add(Error(
            "block_type_invalid",
            $"Unsupported block type '{value}'.",
            $"{path}.{name}"));
        return null;
    }

    private static JsonElement? ReadContent(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (source[name] is not JsonObject content)
        {
            issues.Add(Error(
                "block_content_required",
                $"{name} must be a JSON object.",
                $"{path}.{name}"));
            return null;
        }

        return JsonSerializer.SerializeToElement(content, NotionAggregateJson.Options);
    }

    private static IReadOnlyList<Guid>? ReadGuidArray(
        JsonObject source,
        string name,
        string path,
        List<NotionAggregateIssue> issues)
    {
        if (source[name] is not JsonArray array)
        {
            issues.Add(Error("array_required", $"{name} must be an array.", $"{path}.{name}"));
            return null;
        }

        var result = new List<Guid>();
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is JsonValue value &&
                value.TryGetValue<string>(out var text) &&
                Guid.TryParse(text, out var id) &&
                id != Guid.Empty)
            {
                result.Add(id);
            }
            else
            {
                issues.Add(Error(
                    "guid_required",
                    "Each orderedBlockIds item must be a non-empty GUID string.",
                    $"{path}.{name}[{index}]"));
            }
        }

        return result;
    }

    private static Guid DeterministicId(
        NotionOperationCompileContext context,
        string path)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{context.IdempotencyKey}\n{context.RequestHash}\n{path}"));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[7] = (byte)((id[7] & 0x0f) | 0x50);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }

    private static NotionAggregateIssue UnknownField(string path)
        => Error(
            "unknown_field",
            $"Unknown field '{path[(path.LastIndexOf('.') + 1)..]}'.",
            path);

    private static NotionAggregateIssue Error(string code, string message, string path)
        => new()
        {
            Code = code,
            Severity = NotionIssueSeverity.Error,
            Message = message,
            Path = path
        };
}
