using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Services;

/// <summary>Exports Gantt tasks to an XLSX spreadsheet.</summary>
public static class GanttXlsxExporter
{
    private static readonly GanttColumnKey[] DefaultColumns =
    [
        GanttColumnKey.Title, GanttColumnKey.Start, GanttColumnKey.End,
        GanttColumnKey.Progress, GanttColumnKey.Status, GanttColumnKey.Priority,
        GanttColumnKey.Duration, GanttColumnKey.Assignees
    ];

    public static byte[] Export(
        IEnumerable<GanttTask> tasks,
        IEnumerable<GanttDependency> dependencies,
        GanttExportOptions options)
    {
        var cols = options.Columns?.ToArray() ?? DefaultColumns;
        var taskList = tasks.ToList();

        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var wbp = doc.AddWorkbookPart();
            wbp.Workbook = new Workbook();

            var wsp = wbp.AddNewPart<WorksheetPart>();
            var sd  = new SheetData();
            wsp.Worksheet = new Worksheet(sd);

            wbp.Workbook.AppendChild(new Sheets()).Append(new Sheet
            {
                Id = wbp.GetIdOfPart(wsp),
                SheetId = 1,
                Name = "Tasks"
            });

            // Header row
            sd.Append(BuildRow(cols.Select(c => c.ToString())));

            // Data rows
            foreach (var task in taskList)
                sd.Append(BuildRow(cols.Select(c => GetCellValue(task, c))));

            doc.Save();
        }

        return ms.ToArray();
    }

    private static Row BuildRow(IEnumerable<string> values)
    {
        var row = new Row();
        foreach (var v in values)
            row.Append(new Cell { CellValue = new CellValue(v), DataType = CellValues.InlineString });
        return row;
    }

    private static string GetCellValue(GanttTask task, GanttColumnKey col) => col switch
    {
        GanttColumnKey.Title     => task.Title,
        GanttColumnKey.Start     => task.Start.ToString("yyyy-MM-dd"),
        GanttColumnKey.End       => task.End.ToString("yyyy-MM-dd"),
        GanttColumnKey.Progress  => task.PercentComplete.ToString(),
        GanttColumnKey.Status    => task.Status.ToString(),
        GanttColumnKey.Priority  => task.Priority.ToString(),
        GanttColumnKey.Duration  => ((int)task.Duration.TotalDays).ToString(),
        GanttColumnKey.Deadline  => task.Deadline?.ToString("yyyy-MM-dd") ?? "",
        GanttColumnKey.Estimation => task.EstimationHours?.ToString("F1") ?? "",
        GanttColumnKey.TimeLog   => task.LoggedHours?.ToString("F1") ?? "",
        GanttColumnKey.Assignees => string.Join(", ", task.Assignees.Select(a => a.Name)),
        GanttColumnKey.Comments  => task.Comments.Count.ToString(),
        _                        => ""
    };
}
