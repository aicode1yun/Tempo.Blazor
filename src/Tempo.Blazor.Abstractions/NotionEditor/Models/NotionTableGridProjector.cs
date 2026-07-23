namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Projects logical Notion table cells into a complete physical grid.</summary>
public static class NotionTableGridProjector
{
    /// <summary>
    /// Projects logical cells top-down and left-to-right, returning structured diagnostics instead
    /// of exposing renderer-specific merge markers in the authoring model.
    /// </summary>
    public static bool TryProject(
        IReadOnlyList<NotionAuthoringTableRow>? rows,
        int columnCount,
        string rowsPath,
        out NotionTableGridProjection? projection,
        out IReadOnlyList<NotionAggregateIssue> issues)
    {
        var found = new List<NotionAggregateIssue>();
        projection = null;
        rowsPath = string.IsNullOrWhiteSpace(rowsPath) ? "$.rows" : rowsPath;

        if (rows is null)
        {
            Add("table_rows_required", "Table rows are required.", rowsPath, "Supply a rows array.");
            issues = found;
            return false;
        }

        if (rows.Count > NotionAuthoringLimits.MaxTableRows)
        {
            Add(
                "table_row_limit_exceeded",
                $"A table may contain at most {NotionAuthoringLimits.MaxTableRows} rows.",
                rowsPath,
                "Split the content into multiple tables.");
        }
        if (columnCount < 1 || columnCount > NotionAuthoringLimits.MaxTableColumns)
        {
            var columnPath = rowsPath.EndsWith(".rows", StringComparison.Ordinal)
                ? rowsPath[..^5] + ".columnCount"
                : rowsPath + ".columnCount";
            Add(
                "table_column_limit_exceeded",
                $"columnCount must be between 1 and {NotionAuthoringLimits.MaxTableColumns}.",
                columnPath,
                "Use a supported positive column count.");
        }
        if (rows.Count > 0 &&
            columnCount > 0 &&
            (long)rows.Count * columnCount > NotionAuthoringLimits.MaxTableSlots)
        {
            Add(
                "table_slot_limit_exceeded",
                $"A table may contain at most {NotionAuthoringLimits.MaxTableSlots} physical slots.",
                rowsPath,
                "Reduce the row or column count.");
        }
        if (found.Count > 0)
        {
            issues = found;
            return false;
        }

        var slots = new NotionTableGridSlot?[rows.Count, columnCount];
        var contentLength = 0L;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowPath = $"{rowsPath}[{rowIndex}]";
            var rowIssueCount = found.Count;
            var column = 0;
            if (row is null)
            {
                Add(
                    "table_row_required",
                    "A logical table row cannot be null.",
                    rowPath,
                    "Supply a row object with a cells array.");
                continue;
            }

            var logicalCells = row.Cells ?? [];
            for (var cellIndex = 0; cellIndex < logicalCells.Count; cellIndex++)
            {
                while (column < columnCount && slots[rowIndex, column] is not null)
                {
                    column++;
                }

                var cell = logicalCells[cellIndex];
                var cellPath = $"{rowPath}.cells[{cellIndex}]";
                if (cell is null)
                {
                    Add(
                        "table_cell_required",
                        "A logical table cell cannot be null.",
                        cellPath,
                        "Supply a table cell object.");
                    continue;
                }
                ValidateCell(cell, cellPath, ref contentLength);
                if (cell.RowSpan < 1 || cell.ColumnSpan < 1)
                {
                    continue;
                }
                if (rowIndex + cell.RowSpan > rows.Count)
                {
                    Add(
                        "table_row_span_overflow",
                        "rowSpan extends beyond the available table rows.",
                        $"{cellPath}.rowSpan",
                        "Reduce rowSpan so the cell ends within the table.");
                    continue;
                }
                if (column >= columnCount || column + cell.ColumnSpan > columnCount)
                {
                    Add(
                        "table_span_out_of_range",
                        "columnSpan extends beyond the table width.",
                        $"{cellPath}.columnSpan",
                        "Reduce columnSpan or increase columnCount.");
                    continue;
                }

                var overlaps = false;
                for (var rowOffset = 0; rowOffset < cell.RowSpan && !overlaps; rowOffset++)
                {
                    for (var columnOffset = 0; columnOffset < cell.ColumnSpan; columnOffset++)
                    {
                        if (slots[rowIndex + rowOffset, column + columnOffset] is not null)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                }
                if (overlaps)
                {
                    Add(
                        "table_cell_overlap",
                        "The cell overlaps a slot covered by an earlier merged cell.",
                        $"{cellPath}.columnSpan",
                        "Reduce the span or move the logical cell after the active row span.");
                    continue;
                }

                for (var rowOffset = 0; rowOffset < cell.RowSpan; rowOffset++)
                {
                    for (var columnOffset = 0; columnOffset < cell.ColumnSpan; columnOffset++)
                    {
                        slots[rowIndex + rowOffset, column + columnOffset] = new NotionTableGridSlot
                        {
                            Row = rowIndex + rowOffset,
                            Column = column + columnOffset,
                            OriginRow = rowIndex,
                            OriginColumn = column,
                            LogicalCellIndex = cellIndex,
                            Cell = cell,
                            IsOrigin = rowOffset == 0 && columnOffset == 0
                        };
                    }
                }

                column += cell.ColumnSpan;
            }

            if (found.Count == rowIssueCount &&
                Enumerable.Range(0, columnCount).Any(index => slots[rowIndex, index] is null))
            {
                Add(
                    "table_row_width_mismatch",
                    "Logical cells and active row spans must cover every table column exactly once.",
                    $"{rowPath}.cells",
                    "Add or resize cells so the physical row width equals columnCount.");
            }
        }

        if (contentLength > NotionAuthoringLimits.MaxTableContentLength)
        {
            Add(
                "table_content_limit_exceeded",
                $"Combined table content may contain at most {NotionAuthoringLimits.MaxTableContentLength} characters.",
                rowsPath,
                "Split the content into multiple tables.");
        }

        if (found.Count > 0)
        {
            issues = found;
            return false;
        }

        projection = new NotionTableGridProjection(
            rows.Count,
            columnCount,
            slots.Cast<NotionTableGridSlot>().ToList());
        issues = [];
        return true;

        void ValidateCell(NotionAuthoringTableCell cell, string path, ref long totalLength)
        {
            if (cell.RowSpan < 1)
            {
                Add("table_span_out_of_range", "rowSpan must be at least 1.", $"{path}.rowSpan", "Use a positive rowSpan.");
            }
            if (cell.ColumnSpan < 1)
            {
                Add("table_span_out_of_range", "columnSpan must be at least 1.", $"{path}.columnSpan", "Use a positive columnSpan.");
            }
            if (cell.Width is <= 0 || cell.Width is { } width && !double.IsFinite(width))
            {
                Add("table_width_out_of_range", "Cell width must be a finite positive number or null.", $"{path}.width", "Use a finite positive CSS-pixel width or null.");
            }
            var html = cell.Html ?? string.Empty;
            if (html.Length > NotionAuthoringLimits.MaxCellHtmlLength)
            {
                Add("table_cell_html_limit_exceeded", $"Cell HTML may contain at most {NotionAuthoringLimits.MaxCellHtmlLength} characters.", $"{path}.html", "Shorten the cell or split the table.");
            }
            else if (!NotionHtmlSanitizer.TryNormalizeTableCellHtml(html, out _))
            {
                Add("unsafe_table_cell_html", "Cell HTML contains unsupported or unsafe markup.", $"{path}.html", "Use plain text or the documented inline formatting tags.");
            }
            totalLength += html.Length;

            ValidateColor(cell.BackgroundColor, $"{path}.backgroundColor");
            ValidateColor(cell.TextColor, $"{path}.textColor");
            var inlines = cell.Inlines ?? [];
            if (inlines.Count > NotionAuthoringLimits.MaxCellInlines)
            {
                Add("table_inline_limit_exceeded", $"A cell may contain at most {NotionAuthoringLimits.MaxCellInlines} inlines.", $"{path}.inlines", "Merge adjacent inlines or split the content.");
            }
            for (var inlineIndex = 0; inlineIndex < inlines.Count; inlineIndex++)
            {
                var inline = inlines[inlineIndex];
                var inlinePath = $"{path}.inlines[{inlineIndex}]";
                if (inline is null)
                {
                    Add("table_inline_required", "A structured inline cannot be null.", inlinePath, "Supply a structured inline object.");
                    continue;
                }
                var text = inline.Text ?? string.Empty;
                if (text.Length > NotionAuthoringLimits.MaxInlineTextLength)
                {
                    Add("table_inline_text_limit_exceeded", $"Inline text may contain at most {NotionAuthoringLimits.MaxInlineTextLength} characters.", $"{inlinePath}.text", "Split the text into smaller cells.");
                }
                if (inline.Href is not null && !NotionHtmlSanitizer.IsSafeHref(inline.Href))
                {
                    Add("unsafe_table_inline_href", "Inline href must use http, https or mailto.", $"{inlinePath}.href", "Use an absolute http, https or mailto URL.");
                }
                ValidateColor(inline.TextColor, $"{inlinePath}.textColor");
                ValidateColor(inline.BackgroundColor, $"{inlinePath}.backgroundColor");
                totalLength += text.Length;
            }

            if (cell.Borders is { } borders)
            {
                ValidateBorder(borders.Top, $"{path}.borders.top");
                ValidateBorder(borders.Right, $"{path}.borders.right");
                ValidateBorder(borders.Bottom, $"{path}.borders.bottom");
                ValidateBorder(borders.Left, $"{path}.borders.left");
            }
        }

        void ValidateBorder(NotionTableBorder? border, string path)
        {
            if (border is null)
            {
                return;
            }
            if (!double.IsFinite(border.Width) || border.Width <= 0)
            {
                Add("table_border_width_out_of_range", "Border width must be a finite positive number.", $"{path}.width", "Use a finite positive CSS-pixel width.");
            }
            ValidateColor(border.Color, $"{path}.color");
        }

        void ValidateColor(string? color, string path)
        {
            if (!NotionCssNormalizer.TryNormalizeColor(color, out _))
            {
                Add("unsafe_css_color", "Only literal CSS colors are allowed.", path, "Use a hex, named, rgb/rgba or hsl/hsla literal without url(), var() or separators.");
            }
        }

        void Add(string code, string message, string path, string suggestedFix)
            => found.Add(new NotionAggregateIssue
            {
                Code = code,
                Severity = NotionIssueSeverity.Error,
                Message = message,
                Path = path,
                SuggestedFix = suggestedFix
            });
    }
}

/// <summary>A complete physical table grid derived from logical authoring cells.</summary>
public sealed class NotionTableGridProjection
{
    private readonly IReadOnlyList<NotionTableGridSlot> _slots;

    internal NotionTableGridProjection(
        int rowCount,
        int columnCount,
        IReadOnlyList<NotionTableGridSlot> slots)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
        _slots = slots;
    }

    /// <summary>Number of physical rows.</summary>
    public int RowCount { get; }

    /// <summary>Number of physical columns.</summary>
    public int ColumnCount { get; }

    /// <summary>All physical slots in row-major order.</summary>
    public IReadOnlyList<NotionTableGridSlot> Slots => _slots;

    /// <summary>Returns one physical slot.</summary>
    public NotionTableGridSlot GetSlot(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, RowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, ColumnCount);
        return _slots[(row * ColumnCount) + column];
    }
}

/// <summary>One physical slot and its logical merge origin.</summary>
public sealed class NotionTableGridSlot
{
    /// <summary>Physical row index.</summary>
    public int Row { get; init; }

    /// <summary>Physical column index.</summary>
    public int Column { get; init; }

    /// <summary>Physical row of the logical origin cell.</summary>
    public int OriginRow { get; init; }

    /// <summary>Physical column of the logical origin cell.</summary>
    public int OriginColumn { get; init; }

    /// <summary>Index of the logical cell within its authoring row.</summary>
    public int LogicalCellIndex { get; init; }

    /// <summary>Whether this slot is the rendered origin rather than a covered merge position.</summary>
    public bool IsOrigin { get; init; }

    /// <summary>Logical cell covering this physical slot.</summary>
    public NotionAuthoringTableCell Cell { get; init; } = new();
}
