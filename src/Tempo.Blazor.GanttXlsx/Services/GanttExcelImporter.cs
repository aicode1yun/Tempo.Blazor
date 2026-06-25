using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Abstractions.WorkItems;

namespace Tempo.Blazor.Services;

/// <summary>Imports Gantt tasks from an XLSX spreadsheet stream.</summary>
public static class GanttExcelImporter
{
    private static readonly Dictionary<string, GanttColumnKey> DefaultHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["title"]    = GanttColumnKey.Title,
            ["name"]     = GanttColumnKey.Title,
            ["start"]    = GanttColumnKey.Start,
            ["end"]      = GanttColumnKey.End,
            ["finish"]   = GanttColumnKey.End,
            ["progress"] = GanttColumnKey.Progress,
            ["done"]     = GanttColumnKey.Progress,
            ["status"]   = GanttColumnKey.Status,
            ["priority"] = GanttColumnKey.Priority,
        };

    /// <summary>Imports work items from an XLSX stream using optional column mappings.</summary>
    /// <param name="stream">Readable XLSX stream.</param>
    /// <param name="mappings">Optional source-column to Gantt-property mappings.</param>
    /// <returns>Imported work items.</returns>
    public static IReadOnlyList<TmWorkItem> Import(Stream stream,
        IEnumerable<GanttColumnMapping>? mappings = null)
    {
        var userMappings = mappings?
            .ToDictionary(m => m.SourceColumn, m => m.TargetProperty, StringComparer.OrdinalIgnoreCase);

        using var doc = SpreadsheetDocument.Open(stream, false);
        var wbp = doc.WorkbookPart ?? throw new InvalidOperationException("Missing WorkbookPart.");
        var wsp = wbp.WorksheetParts.First();
        var rows = wsp.Worksheet.Descendants<Row>().ToList();
        if (rows.Count < 2) return [];

        var sharedStrings = wbp.SharedStringTablePart?.SharedStringTable;
        var headers = ReadRow(rows[0], sharedStrings);

        // Build column-index → GanttColumnKey map
        var colMap = new Dictionary<int, GanttColumnKey>();
        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (userMappings is not null && userMappings.TryGetValue(header, out var mappedKey))
                colMap[i] = mappedKey;
            else if (DefaultHeaders.TryGetValue(header, out var defaultKey))
                colMap[i] = defaultKey;
        }

        var result = new List<TmWorkItem>();
        for (var rowIdx = 1; rowIdx < rows.Count; rowIdx++)
        {
            var cells = ReadRow(rows[rowIdx], sharedStrings);
            var task = new TmWorkItem();
            for (var col = 0; col < cells.Count; col++)
            {
                if (!colMap.TryGetValue(col, out var key)) continue;
                var val = cells[col];
                switch (key)
                {
                    case GanttColumnKey.Title:
                        task.Title = val; break;
                    case GanttColumnKey.Start:
                        if (TryParseDate(val, out var s)) task.Start = s; break;
                    case GanttColumnKey.End:
                        if (TryParseDate(val, out var e)) task.End = e; break;
                    case GanttColumnKey.Progress:
                        if (int.TryParse(val, out var p)) task.PercentComplete = Math.Clamp(p, 0, 100); break;
                    case GanttColumnKey.Status:
                        if (Enum.TryParse<TmWorkItemStatus>(val, true, out var st)) task.Status = st; break;
                    case GanttColumnKey.Priority:
                        if (Enum.TryParse<TmWorkItemPriority>(val, true, out var pr)) task.Priority = pr; break;
                }
            }
            if (!string.IsNullOrWhiteSpace(task.Title))
                result.Add(task);
        }
        return result;
    }

    private static List<string> ReadRow(Row row, SharedStringTable? sst)
    {
        var values = new List<string>();
        foreach (var cell in row.Descendants<Cell>())
        {
            var raw = cell.CellValue?.Text ?? "";
            if (cell.DataType?.Value == CellValues.SharedString && sst is not null &&
                int.TryParse(raw, out var idx))
                raw = sst.ElementAt(idx).InnerText;
            else if (cell.DataType?.Value == CellValues.InlineString)
                raw = cell.InlineString?.Text?.Text ?? raw;
            values.Add(raw);
        }
        return values;
    }

    private static bool TryParseDate(string value, out DateTime result)
    {
        string[] formats = ["yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy", "yyyy-MM-ddTHH:mm:ss"];
        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out result);
    }
}
