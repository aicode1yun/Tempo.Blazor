using System.Globalization;
using System.Xml.Linq;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Services;

/// <summary>Imports Gantt tasks from a Microsoft Project XML (MPP XML) stream.</summary>
public static class GanttMppImporter
{
    private static readonly XNamespace MsNs = "http://schemas.microsoft.com/project";

    public static IReadOnlyList<TmWorkItem> Import(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var tasksEl = doc.Root?.Element(MsNs + "Tasks");
        if (tasksEl is null) return [];

        var result = new List<TmWorkItem>();
        foreach (var taskEl in tasksEl.Elements(MsNs + "Task"))
        {
            var name = taskEl.Element(MsNs + "Name")?.Value;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var task = new TmWorkItem { Title = name };

            var startStr  = taskEl.Element(MsNs + "Start")?.Value;
            var finishStr = taskEl.Element(MsNs + "Finish")?.Value;
            var pctStr    = taskEl.Element(MsNs + "PercentComplete")?.Value;

            if (TryParseDateTime(startStr, out var start))   task.Start = start;
            if (TryParseDateTime(finishStr, out var finish))  task.End   = finish;
            if (int.TryParse(pctStr, out var pct))            task.PercentComplete = Math.Clamp(pct, 0, 100);

            result.Add(task);
        }
        return result;
    }

    private static bool TryParseDateTime(string? value, out DateTime result)
    {
        if (string.IsNullOrEmpty(value)) { result = default; return false; }
        string[] formats = ["yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"];
        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out result);
    }
}
