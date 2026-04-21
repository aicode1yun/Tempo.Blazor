namespace Tempo.Blazor.NotionEditor.Interfaces;

public interface INotionPage
{
    Guid Id { get; }
    Guid? ParentId { get; }
    string Title { get; }
    string? Description { get; }
    string? IconEmoji { get; }
    string? IconImageUrl { get; }
    string? CoverImageUrl { get; }
    double? CoverImagePositionY { get; }
    bool IsFullWidth { get; }
    bool IsSmallText { get; }
    bool IsLocked { get; }
    DateTime CreatedAt { get; }
    string? CreatedByUserId { get; }
    DateTime LastEditedAt { get; }
    string? LastEditedByUserId { get; }
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
    bool IsFavorite { get; }
}
