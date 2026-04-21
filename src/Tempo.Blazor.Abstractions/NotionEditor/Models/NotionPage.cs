namespace Tempo.Blazor.NotionEditor.Interfaces;

public class NotionPage : INotionPage
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconEmoji { get; set; }
    public string? IconImageUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public double? CoverImagePositionY { get; set; }
    public bool IsFullWidth { get; set; }
    public bool IsSmallText { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTime LastEditedAt { get; set; } = DateTime.UtcNow;
    public string? LastEditedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsFavorite { get; set; }
}
