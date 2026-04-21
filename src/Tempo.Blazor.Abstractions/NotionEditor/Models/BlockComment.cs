namespace Tempo.Blazor.NotionEditor.Models;

public class BlockComment : IBlockComment
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public IReadOnlyList<INotionCommentEntry> Thread { get; set; } = new List<INotionCommentEntry>();
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
