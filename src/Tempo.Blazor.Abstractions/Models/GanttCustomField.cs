namespace Tempo.Blazor.Abstractions.Models;

/// <summary>
/// Defines a user-configurable custom field that can be added to all Gantt tasks.
/// </summary>
public class GanttCustomField
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public GanttFieldType Type { get; set; } = GanttFieldType.Text;

    /// <summary>Predefined options for List/Multiselect/Labels field types.</summary>
    public IReadOnlyList<string>? Options { get; set; }
}
