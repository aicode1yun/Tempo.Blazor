namespace Tempo.Blazor.Abstractions.Models;

public class GanttReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public GanttReportType Type { get; set; }
    public Dictionary<string, string> Config { get; set; } = [];
}
