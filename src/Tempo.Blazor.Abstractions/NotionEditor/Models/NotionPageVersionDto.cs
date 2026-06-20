using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.NotionEditor.Models;

/// <summary>Serializable page history version transferred between the Demo API and Blazor clients.</summary>
public sealed class NotionPageVersionDto : IPageVersion
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string? EditedByUserId { get; set; }
    public string EditedByDisplayName { get; set; } = string.Empty;
    public List<PageBlock> BlocksSnapshot { get; set; } = [];
    public string? ChangeDescription { get; set; }

    IReadOnlyList<IPageBlock> IPageVersion.BlocksSnapshot => BlocksSnapshot;
}
