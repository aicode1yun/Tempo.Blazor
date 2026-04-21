namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

public class PageBlock : IPageBlock
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid? ParentBlockId { get; set; }
    public BlockType Type { get; set; }
    public int Order { get; set; }
    public IBlockContent Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastEditedAt { get; set; } = DateTime.UtcNow;
}
