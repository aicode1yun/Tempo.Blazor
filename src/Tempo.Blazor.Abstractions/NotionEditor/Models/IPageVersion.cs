namespace Tempo.Blazor.NotionEditor.Models;

using Tempo.Blazor.NotionEditor.Interfaces;

public interface IPageVersion
{
    Guid Id { get; }
    Guid PageId { get; }
    DateTime EditedAt { get; }
    string? EditedByUserId { get; }
    string EditedByDisplayName { get; }
    IReadOnlyList<IPageBlock> BlocksSnapshot { get; }
    string? ChangeDescription { get; }
}
