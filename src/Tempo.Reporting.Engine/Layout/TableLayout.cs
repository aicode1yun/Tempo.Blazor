#pragma warning disable MA0048

using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Engine.Expressions;
using Tempo.Reporting.Engine.Fonts;
using Tempo.Reporting.Engine.Processing;
using Tempo.Reporting.Engine.Snapshot;

namespace Tempo.Reporting.Engine.Layout;

/// <summary>Rendered table row kind.</summary>
public enum ReportTableLayoutRowKind
{
    /// <summary>Table header row.</summary>
    Header,

    /// <summary>Group header row.</summary>
    GroupHeader,

    /// <summary>Detail data row.</summary>
    Detail,

    /// <summary>Group footer row.</summary>
    GroupFooter,

    /// <summary>Table footer row.</summary>
    Footer,
}

/// <summary>Table layout request.</summary>
public sealed record ReportTableLayoutRequest
{
    /// <summary>Table definition.</summary>
    public ReportTableElement Table { get; init; } = new();

    /// <summary>Data rows consumed by the table.</summary>
    public ProcessedDataSet DataSet { get; init; } = new(string.Empty, [], []);

    /// <summary>Processing context used for expression evaluation.</summary>
    public ReportProcessingContext Context { get; init; } = new(new("tenant", "user", "en-US"));

    /// <summary>Reusable styles from the report definition.</summary>
    public IReadOnlyList<ReportStyleDefinition> Styles { get; init; } = [];

    /// <summary>Left coordinate relative to the hosting band.</summary>
    public double X { get; init; }

    /// <summary>Top coordinate relative to a table page slice.</summary>
    public double Y { get; init; }

    /// <summary>Table width. Defaults to the element width.</summary>
    public double Width { get; init; }

    /// <summary>Available height on the first page.</summary>
    public double FirstPageHeight { get; init; } = double.PositiveInfinity;

    /// <summary>Available height on continued pages.</summary>
    public double PageHeight { get; init; } = double.PositiveInfinity;
}

/// <summary>Computed table layout.</summary>
public sealed record ReportTableLayout
{
    /// <summary>Creates a table layout.</summary>
    public ReportTableLayout(
        ReportTableElement table,
        IReadOnlyList<ReportTableColumnLayout> columns,
        IReadOnlyList<ReportTableLayoutPage> pages,
        double totalHeight)
    {
        Table = table;
        Columns = columns.ToArray();
        Pages = pages.ToArray();
        TotalHeight = totalHeight;
    }

    /// <summary>Source table definition.</summary>
    public ReportTableElement Table { get; }

    /// <summary>Computed column rectangles.</summary>
    public IReadOnlyList<ReportTableColumnLayout> Columns { get; }

    /// <summary>Paginated table row slices.</summary>
    public IReadOnlyList<ReportTableLayoutPage> Pages { get; }

    /// <summary>Total height of all row slices including repeated headers.</summary>
    public double TotalHeight { get; }
}

/// <summary>Computed table column.</summary>
public sealed record ReportTableColumnLayout(int Index, double X, double Width);

/// <summary>One page slice of a table layout.</summary>
public sealed record ReportTableLayoutPage
{
    /// <summary>Creates a table page slice.</summary>
    public ReportTableLayoutPage(
        int pageIndex,
        IReadOnlyList<ReportTableLayoutRow> rows,
        double height,
        IReadOnlyList<ReportTableColumnLayout> columns,
        ReportTableBorderModel borderModel)
    {
        PageIndex = pageIndex;
        Rows = rows.ToArray();
        Height = height;
        Columns = columns.ToArray();
        BorderModel = borderModel;
    }

    /// <summary>Zero-based table page slice index.</summary>
    public int PageIndex { get; }

    /// <summary>Rows visible in this table page slice.</summary>
    public IReadOnlyList<ReportTableLayoutRow> Rows { get; }

    /// <summary>Height consumed by the slice.</summary>
    public double Height { get; }

    /// <summary>Column rectangles reused by this slice.</summary>
    public IReadOnlyList<ReportTableColumnLayout> Columns { get; }

    /// <summary>Border rendering behavior.</summary>
    public ReportTableBorderModel BorderModel { get; }
}

/// <summary>Computed table row.</summary>
public sealed record ReportTableLayoutRow
{
    /// <summary>Creates a table row.</summary>
    public ReportTableLayoutRow(
        ReportTableLayoutRowKind kind,
        double x,
        double y,
        double width,
        double height,
        string? backgroundColor,
        IReadOnlyList<ReportTableLayoutCell> cells,
        string? groupName = null,
        object? groupKey = null,
        bool repeated = false,
        bool keepWithNext = false)
    {
        Kind = kind;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        BackgroundColor = backgroundColor;
        Cells = cells.ToArray();
        GroupName = groupName;
        GroupKey = groupKey;
        Repeated = repeated;
        KeepWithNext = keepWithNext;
    }

    /// <summary>Row kind.</summary>
    public ReportTableLayoutRowKind Kind { get; }

    /// <summary>Row left coordinate relative to the table origin.</summary>
    public double X { get; }

    /// <summary>Row top coordinate relative to the table page slice origin.</summary>
    public double Y { get; }

    /// <summary>Row width.</summary>
    public double Width { get; }

    /// <summary>Row height.</summary>
    public double Height { get; }

    /// <summary>Optional row background color.</summary>
    public string? BackgroundColor { get; }

    /// <summary>Computed cells.</summary>
    public IReadOnlyList<ReportTableLayoutCell> Cells { get; }

    /// <summary>Group name, when this row belongs to a group header or footer.</summary>
    public string? GroupName { get; }

    /// <summary>Group key, when this row belongs to a group header or footer.</summary>
    public object? GroupKey { get; }

    /// <summary>Whether this row is a repeated table header.</summary>
    public bool Repeated { get; }

    /// <summary>Whether this row should stay with the next row during pagination.</summary>
    public bool KeepWithNext { get; }

    /// <summary>Creates a copy with page-slice coordinates.</summary>
    public ReportTableLayoutRow WithY(double y, bool repeated = false)
        => new(Kind, X, y, Width, Height, BackgroundColor, Cells.Select(cell => cell.WithY(y)).ToArray(), GroupName, GroupKey, repeated, KeepWithNext);
}

/// <summary>Computed table cell.</summary>
public sealed record ReportTableLayoutCell
{
    /// <summary>Creates a table cell.</summary>
    public ReportTableLayoutCell(
        int columnIndex,
        double x,
        double y,
        double width,
        double height,
        string text,
        ReportTextStyle textStyle,
        ReportThickness padding,
        ReportBorder? border,
        string? backgroundColor,
        ReportHorizontalAlignment horizontalAlignment,
        bool canGrow)
    {
        ColumnIndex = columnIndex;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Text = text;
        TextStyle = textStyle;
        Padding = padding;
        Border = border;
        BackgroundColor = backgroundColor;
        HorizontalAlignment = horizontalAlignment;
        CanGrow = canGrow;
    }

    /// <summary>Column index.</summary>
    public int ColumnIndex { get; }

    /// <summary>Cell left coordinate relative to the table origin.</summary>
    public double X { get; }

    /// <summary>Cell top coordinate relative to the table page slice origin.</summary>
    public double Y { get; }

    /// <summary>Cell width.</summary>
    public double Width { get; }

    /// <summary>Cell height.</summary>
    public double Height { get; }

    /// <summary>Evaluated cell text.</summary>
    public string Text { get; }

    /// <summary>Cell text style.</summary>
    public ReportTextStyle TextStyle { get; }

    /// <summary>Cell padding.</summary>
    public ReportThickness Padding { get; }

    /// <summary>Cell border.</summary>
    public ReportBorder? Border { get; }

    /// <summary>Optional cell background.</summary>
    public string? BackgroundColor { get; }

    /// <summary>Cell text alignment.</summary>
    public ReportHorizontalAlignment HorizontalAlignment { get; }

    /// <summary>Whether cell text can grow vertically.</summary>
    public bool CanGrow { get; }

    /// <summary>Creates a copy with page-slice y coordinates.</summary>
    public ReportTableLayoutCell WithY(double y)
        => new(ColumnIndex, X, y, Width, Height, Text, TextStyle, Padding, Border, BackgroundColor, HorizontalAlignment, CanGrow);

    /// <summary>Creates a copy with a row-resolved height.</summary>
    public ReportTableLayoutCell WithHeight(double height)
        => new(ColumnIndex, X, Y, Width, height, Text, TextStyle, Padding, Border, BackgroundColor, HorizontalAlignment, CanGrow);
}

/// <summary>Lays out report tablix elements.</summary>
public static class ReportTableLayouter
{
    private static readonly ReportBorder DefaultCellBorder = ReportBorder.All("#e5e7eb", 0.75);
    private static readonly ReportThickness DefaultCellPadding = new(4, 3, 4, 3);

    /// <summary>Lays out a table into one or more page slices.</summary>
    public static ReportTableLayout Layout(ReportTableLayoutRequest request, ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(measurer);

        var tableWidth = request.Width > 0 ? request.Width : request.Table.Width;
        var columns = ResolveColumns(request.Table, request.X, tableWidth);
        var rows = BuildRows(request, columns, measurer);
        var pages = PaginateRows(request, rows, columns);
        return new ReportTableLayout(
            request.Table,
            columns,
            pages,
            pages.Sum(page => page.Height));
    }

    /// <summary>Creates snapshot commands for a table page slice.</summary>
    public static IEnumerable<ReportSnapshotCommand> ToSnapshotCommands(
        ReportTableLayoutPage page,
        double originX,
        double originY,
        string idPrefix,
        ITextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(measurer);

        foreach (var command in CreateBackgroundCommands(page, originX, originY, idPrefix))
        {
            yield return command;
        }

        var textIndex = 0;
        for (var rowIndex = 0; rowIndex < page.Rows.Count; rowIndex++)
        {
            var row = page.Rows[rowIndex];
            foreach (var cell in row.Cells.Where(cell => cell.Text.Length > 0))
            {
                var layout = ReportTextBoxLayouter.Layout(
                    new ReportTextBoxLayoutRequest
                    {
                        Id = $"{idPrefix}-r{rowIndex:000}-c{cell.ColumnIndex:000}",
                        X = originX + cell.X,
                        Y = originY + cell.Y,
                        Width = cell.Width,
                        Height = cell.Height,
                        Padding = cell.Padding,
                        HorizontalAlignment = cell.HorizontalAlignment,
                        LineSpacing = cell.TextStyle.LineHeight,
                        CanGrow = cell.CanGrow,
                        Runs = [new ReportRichTextRun(cell.Text, cell.TextStyle)],
                    },
                    measurer);
                foreach (var command in layout.ToSnapshotCommands())
                {
                    yield return command;
                }

                textIndex++;
            }
        }

        foreach (var command in CreateBorderCommands(page, originX, originY, idPrefix))
        {
            yield return command;
        }
    }

    private static IReadOnlyList<ReportSnapshotCommand> CreateBackgroundCommands(
        ReportTableLayoutPage page,
        double originX,
        double originY,
        string idPrefix)
    {
        var commands = new List<ReportSnapshotCommand>();
        var index = 0;
        foreach (var row in page.Rows)
        {
            if (!string.IsNullOrWhiteSpace(row.BackgroundColor))
            {
                commands.Add(ReportSnapshotCommand.Rectangle(
                    $"{idPrefix}-row-bg-{index:000}",
                    originX + row.X,
                    originY + row.Y,
                    row.Width,
                    row.Height,
                    row.BackgroundColor));
            }

            foreach (var cell in row.Cells.Where(cell => !string.IsNullOrWhiteSpace(cell.BackgroundColor)))
            {
                commands.Add(ReportSnapshotCommand.Rectangle(
                    $"{idPrefix}-cell-bg-{index:000}-{cell.ColumnIndex:000}",
                    originX + cell.X,
                    originY + cell.Y,
                    cell.Width,
                    cell.Height,
                    cell.BackgroundColor!));
            }

            index++;
        }

        return commands;
    }

    private static IEnumerable<ReportSnapshotCommand> CreateBorderCommands(
        ReportTableLayoutPage page,
        double originX,
        double originY,
        string idPrefix)
    {
        if (page.Rows.Count == 0)
        {
            yield break;
        }

        if (page.BorderModel == ReportTableBorderModel.Separate)
        {
            var index = 0;
            foreach (var cell in page.Rows.SelectMany(row => row.Cells))
            {
                var border = FirstBorderLine(cell.Border);
                if (border is not null)
                {
                    yield return ReportSnapshotCommand.Rectangle(
                        $"{idPrefix}-cell-border-{index:000}",
                        originX + cell.X,
                        originY + cell.Y,
                        cell.Width,
                        cell.Height,
                        string.Empty,
                        border.Color,
                        border.Width);
                }

                index++;
            }

            yield break;
        }

        var line = FirstBorderLine(page.Rows.SelectMany(row => row.Cells).Select(cell => cell.Border).FirstOrDefault(border => border is not null)) ??
            new ReportBorderLine("#e5e7eb", 0.75);
        var left = page.Columns.First().X;
        var right = page.Columns.Last().X + page.Columns.Last().Width;
        var top = page.Rows.First().Y;
        var bottom = page.Rows.Last().Y + page.Rows.Last().Height;
        var gridIndex = 0;

        foreach (var x in page.Columns.Select(column => column.X).Append(right).Distinct().OrderBy(value => value))
        {
            yield return ReportSnapshotCommand.Line(
                $"{idPrefix}-grid-v-{gridIndex:000}",
                originX + x,
                originY + top,
                0,
                bottom - top,
                line.Color,
                line.Width);
            gridIndex++;
        }

        foreach (var y in page.Rows.Select(row => row.Y).Append(bottom).Distinct().OrderBy(value => value))
        {
            yield return ReportSnapshotCommand.Line(
                $"{idPrefix}-grid-h-{gridIndex:000}",
                originX + left,
                originY + y,
                right - left,
                0,
                line.Color,
                line.Width);
            gridIndex++;
        }
    }

    private static IReadOnlyList<ReportTableColumnLayout> ResolveColumns(ReportTableElement table, double x, double width)
    {
        if (table.Columns.Count == 0)
        {
            return [new ReportTableColumnLayout(0, x, width)];
        }

        var fixedWidth = table.Columns
            .Where(column => column.WidthMode == ReportTableColumnWidthMode.Fixed)
            .Sum(column => Math.Max(0, column.Width));
        var proportionalColumns = table.Columns
            .Where(column => column.WidthMode == ReportTableColumnWidthMode.Proportional)
            .ToArray();
        var remaining = Math.Max(0, width - fixedWidth);
        var totalWeight = proportionalColumns.Sum(column => Math.Max(0.0001, column.Width));
        var cursor = x;
        var columns = new List<ReportTableColumnLayout>(table.Columns.Count);
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var columnWidth = column.WidthMode == ReportTableColumnWidthMode.Proportional
                ? remaining * Math.Max(0.0001, column.Width) / Math.Max(0.0001, totalWeight)
                : Math.Max(0, column.Width);
            columns.Add(new ReportTableColumnLayout(index, cursor, columnWidth));
            cursor += columnWidth;
        }

        return columns;
    }

    private static IReadOnlyList<ReportTableLayoutRow> BuildRows(
        ReportTableLayoutRequest request,
        IReadOnlyList<ReportTableColumnLayout> columns,
        ITextMeasurer measurer)
    {
        var rows = new List<ReportTableLayoutRow>();
        if (request.Table.Header is not null)
        {
            rows.Add(CreateRow(request, request.Table.Header, ReportTableLayoutRowKind.Header, null, request.DataSet.Rows, columns, measurer));
        }

        var detailIndex = 0;
        if (request.Table.Groups.Count == 0)
        {
            foreach (var row in request.DataSet.Rows)
            {
                AddDetailRow(request, row, request.DataSet.Rows, columns, measurer, rows, ref detailIndex);
            }
        }
        else
        {
            AddGroupedRows(request, request.DataSet.Rows, 0, columns, measurer, rows, ref detailIndex);
        }

        if (request.Table.Footer is not null)
        {
            rows.Add(CreateRow(request, request.Table.Footer, ReportTableLayoutRowKind.Footer, request.DataSet.Rows.LastOrDefault(), request.DataSet.Rows, columns, measurer));
        }

        return rows;
    }

    private static void AddGroupedRows(
        ReportTableLayoutRequest request,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        int groupIndex,
        IReadOnlyList<ReportTableColumnLayout> columns,
        ITextMeasurer measurer,
        List<ReportTableLayoutRow> output,
        ref int detailIndex)
    {
        var groupDefinition = request.Table.Groups[groupIndex];
        foreach (var group in GroupRows(scopeRows, groupDefinition.Expression, request.Context))
        {
            if (groupDefinition.Header is not null)
            {
                output.Add(CreateRow(
                    request,
                    groupDefinition.Header,
                    ReportTableLayoutRowKind.GroupHeader,
                    group.Rows.FirstOrDefault(),
                    group.Rows,
                    columns,
                    measurer,
                    groupDefinition.Name,
                    group.Key,
                    keepWithNext: groupDefinition.KeepWithFirstDetail));
            }

            if (groupIndex + 1 < request.Table.Groups.Count)
            {
                AddGroupedRows(request, group.Rows, groupIndex + 1, columns, measurer, output, ref detailIndex);
            }
            else
            {
                foreach (var row in group.Rows)
                {
                    AddDetailRow(request, row, group.Rows, columns, measurer, output, ref detailIndex);
                }
            }

            if (groupDefinition.Footer is not null)
            {
                output.Add(CreateRow(
                    request,
                    groupDefinition.Footer,
                    ReportTableLayoutRowKind.GroupFooter,
                    group.Rows.LastOrDefault(),
                    group.Rows,
                    columns,
                    measurer,
                    groupDefinition.Name,
                    group.Key));
            }
        }
    }

    private static void AddDetailRow(
        ReportTableLayoutRequest request,
        ProcessedDataRow row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        IReadOnlyList<ReportTableColumnLayout> columns,
        ITextMeasurer measurer,
        List<ReportTableLayoutRow> output,
        ref int detailIndex)
    {
        if (!IsVisible(request.Table.Detail.VisibleExpression, row, scopeRows, request))
        {
            return;
        }

        var layoutRow = CreateRow(
            request,
            request.Table.Detail,
            ReportTableLayoutRowKind.Detail,
            row,
            scopeRows,
            columns,
            measurer,
            detailIndex: detailIndex);
        output.Add(layoutRow);
        detailIndex++;
    }

    private static ReportTableLayoutRow CreateRow(
        ReportTableLayoutRequest request,
        ReportTableRow row,
        ReportTableLayoutRowKind kind,
        ProcessedDataRow? currentRow,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        IReadOnlyList<ReportTableColumnLayout> columns,
        ITextMeasurer measurer,
        string? groupName = null,
        object? groupKey = null,
        bool keepWithNext = false,
        int detailIndex = 0)
    {
        var rowBackground = ResolveRowBackground(request, row, kind, currentRow, scopeRows, detailIndex);
        var cells = new List<ReportTableLayoutCell>(columns.Count);
        var rowHeight = Math.Max(1, row.Height);
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var cell = index < row.Cells.Count ? row.Cells[index] : new ReportTableCell();
            var cellLayout = CreateCell(request, row, cell, column, currentRow, scopeRows, measurer, rowHeight, rowBackground);
            rowHeight = Math.Max(rowHeight, cellLayout.Height);
            cells.Add(cellLayout);
        }

        cells = cells
            .Select(cell => Math.Abs(cell.Height - rowHeight) < 0.0001 ? cell : cell.WithHeight(rowHeight))
            .ToList();

        return new ReportTableLayoutRow(
            kind,
            request.X,
            request.Y,
            columns.Sum(column => column.Width),
            rowHeight,
            rowBackground,
            cells,
            groupName,
            groupKey,
            repeated: false,
            keepWithNext: keepWithNext || row.KeepTogether && kind == ReportTableLayoutRowKind.GroupHeader);
    }

    private static ReportTableLayoutCell CreateCell(
        ReportTableLayoutRequest request,
        ReportTableRow row,
        ReportTableCell cell,
        ReportTableColumnLayout column,
        ProcessedDataRow? currentRow,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ITextMeasurer measurer,
        double rowHeight,
        string? rowBackground)
    {
        var nestedTextBox = cell.Elements.OfType<ReportTextBoxElement>().FirstOrDefault();
        var style = ResolveStyle(request.Styles, cell.StyleId ?? nestedTextBox?.StyleId);
        var textStyle = style?.Text ?? nestedTextBox?.TextStyle ?? cell.TextStyle;
        var padding = nestedTextBox?.Padding ?? cell.Padding ?? style?.Padding ?? DefaultCellPadding;
        var border = nestedTextBox?.Border ?? cell.Border ?? style?.Border ?? DefaultCellBorder;
        var text = ResolveCellText(cell, nestedTextBox, currentRow, scopeRows, request);
        var horizontalAlignment = nestedTextBox?.HorizontalAlignment ?? cell.HorizontalAlignment;
        var canGrow = nestedTextBox?.CanGrow ?? cell.CanGrow;
        var background = cell.BackgroundColor ?? style?.FillColor;
        var layout = ReportTextBoxLayouter.Layout(
            new ReportTextBoxLayoutRequest
            {
                Id = "table-cell-measure",
                X = 0,
                Y = 0,
                Width = column.Width,
                Height = rowHeight,
                Padding = padding,
                HorizontalAlignment = horizontalAlignment,
                LineSpacing = textStyle.LineHeight,
                CanGrow = canGrow,
                Runs = [new ReportRichTextRun(text, textStyle)],
            },
            measurer);

        return new ReportTableLayoutCell(
            column.Index,
            column.X,
            request.Y,
            column.Width,
            Math.Max(rowHeight, layout.ActualHeight),
            text,
            textStyle,
            padding,
            border,
            background ?? rowBackground,
            horizontalAlignment,
            canGrow);
    }

    private static IReadOnlyList<ReportTableLayoutPage> PaginateRows(
        ReportTableLayoutRequest request,
        IReadOnlyList<ReportTableLayoutRow> rows,
        IReadOnlyList<ReportTableColumnLayout> columns)
    {
        var pages = new List<ReportTableLayoutPage>();
        var current = new List<ReportTableLayoutRow>();
        var currentHeight = 0d;
        var pageIndex = 0;
        var header = rows.FirstOrDefault(row => row.Kind == ReportTableLayoutRowKind.Header);
        var pageLimit = ResolvePageLimit(request.FirstPageHeight);

        foreach (var row in rows)
        {
            var required = RequiredHeightWithKeepWithNext(rows, row);
            if (current.Count > 0 && currentHeight + required > pageLimit + 0.0001)
            {
                AddPage();
                pageLimit = ResolvePageLimit(request.PageHeight);
                if (request.Table.RepeatHeaderOnNewPage && header is not null && row.Kind != ReportTableLayoutRowKind.Header)
                {
                    AddRow(header.WithY(request.Y, repeated: true));
                }
            }

            AddRow(row.WithY(request.Y + currentHeight));
        }

        AddPage();
        return pages;

        void AddRow(ReportTableLayoutRow row)
        {
            current.Add(row);
            currentHeight += row.Height;
        }

        void AddPage()
        {
            if (current.Count == 0)
            {
                pageIndex++;
                return;
            }

            pages.Add(new ReportTableLayoutPage(pageIndex, current.ToArray(), currentHeight, columns, request.Table.BorderModel));
            current = [];
            currentHeight = 0;
            pageIndex++;
        }
    }

    private static double RequiredHeightWithKeepWithNext(IReadOnlyList<ReportTableLayoutRow> rows, ReportTableLayoutRow row)
    {
        if (!row.KeepWithNext)
        {
            return row.Height;
        }

        var index = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row))
            {
                index = i;
                break;
            }
        }

        return index >= 0 && index + 1 < rows.Count
            ? row.Height + rows[index + 1].Height
            : row.Height;
    }

    private static double ResolvePageLimit(double value)
        => double.IsInfinity(value) || value <= 0 ? double.MaxValue / 4 : value;

    private static string ResolveCellText(
        ReportTableCell cell,
        ReportTextBoxElement? nestedTextBox,
        ProcessedDataRow? currentRow,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportTableLayoutRequest request)
    {
        var expression = nestedTextBox?.Expression ?? cell.Expression;
        if (!string.IsNullOrWhiteSpace(expression))
        {
            return ReportAggregateEngine
                .EvaluateForRow(expression, currentRow, scopeRows, request.Context, request.DataSet.Rows)
                .AsString();
        }

        return nestedTextBox?.Text ?? cell.Text ?? string.Empty;
    }

    private static string? ResolveRowBackground(
        ReportTableLayoutRequest request,
        ReportTableRow row,
        ReportTableLayoutRowKind kind,
        ProcessedDataRow? currentRow,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        int detailIndex)
    {
        if (!string.IsNullOrWhiteSpace(row.BackgroundExpression))
        {
            var value = ReportAggregateEngine.EvaluateForRow(row.BackgroundExpression, currentRow, scopeRows, request.Context, request.DataSet.Rows);
            if (value.Kind == ExpressionValueKind.String)
            {
                return value.AsString();
            }

            if (value.Kind == ExpressionValueKind.Boolean && value.AsBoolean())
            {
                return request.Table.ZebraStripeColor;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.BackgroundColor))
        {
            return row.BackgroundColor;
        }

        return kind == ReportTableLayoutRowKind.Detail && detailIndex % 2 == 1
            ? request.Table.ZebraStripeColor
            : null;
    }

    private static bool IsVisible(
        string? expression,
        ProcessedDataRow? row,
        IReadOnlyList<ProcessedDataRow> scopeRows,
        ReportTableLayoutRequest request)
        => string.IsNullOrWhiteSpace(expression) ||
            ReportAggregateEngine.EvaluateForRow(expression, row, scopeRows, request.Context, request.DataSet.Rows).AsBoolean();

    private static IReadOnlyList<TableGroup> GroupRows(
        IReadOnlyList<ProcessedDataRow> rows,
        string expression,
        ReportProcessingContext context)
    {
        var groups = new List<TableGroup>();
        foreach (var row in rows)
        {
            var key = ReportAggregateEngine.EvaluateForRow(expression, row, [row], context, rows).RawValue;
            var group = groups.FirstOrDefault(candidate => Equals(candidate.Key, key));
            if (group is null)
            {
                group = new TableGroup(key);
                groups.Add(group);
            }

            group.Rows.Add(row);
        }

        return groups;
    }

    private static ReportStyleDefinition? ResolveStyle(IReadOnlyList<ReportStyleDefinition> styles, string? styleId)
        => string.IsNullOrWhiteSpace(styleId)
            ? null
            : styles.FirstOrDefault(style => string.Equals(style.Id, styleId, StringComparison.Ordinal));

    private static ReportBorderLine? FirstBorderLine(ReportBorder? border)
        => border?.Top ?? border?.Right ?? border?.Bottom ?? border?.Left;

    private sealed class TableGroup
    {
        public TableGroup(object? key)
        {
            Key = key;
        }

        public object? Key { get; }

        public List<ProcessedDataRow> Rows { get; } = [];
    }
}

#pragma warning restore MA0048
