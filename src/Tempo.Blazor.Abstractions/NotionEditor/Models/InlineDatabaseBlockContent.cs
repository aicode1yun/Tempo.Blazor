namespace Tempo.Blazor.NotionEditor.Models;

public class InlineDatabaseBlockContent : IInlineDatabaseBlockContent
{
    public Guid DatabaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? IconEmoji { get; set; }
    public IReadOnlyList<IDatabaseField> Fields { get; set; } = new List<IDatabaseField>();
    public IReadOnlyList<IDatabaseView> Views { get; set; } = new List<IDatabaseView>();
    public Guid ActiveViewId { get; set; }
}
