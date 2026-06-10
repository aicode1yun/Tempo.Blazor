namespace Tempo.Blazor.NotionEditor.Models;

using System.Text.Json.Serialization;
using Tempo.Blazor.NotionEditor.Enums;

public class TodoBlockContent : ITodoBlockContent
{
    public bool IsChecked { get; set; }
    public string? AssigneeId { get; set; }
    public string? AssigneeDisplayName { get; set; }
    public DateTime? DueDate { get; set; }

    [JsonIgnore]
    public bool IsOverdue
    {
        get => !IsChecked && DueDate is DateTime dueDate && dueDate.Date < DateTime.Today;
        set { }
    }

    public string Html { get; set; } = string.Empty;
    public IReadOnlyList<Mention> Mentions { get; set; } = new List<Mention>();
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
}
