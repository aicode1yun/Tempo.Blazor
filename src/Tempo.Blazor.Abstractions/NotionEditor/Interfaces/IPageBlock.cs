namespace Tempo.Blazor.NotionEditor.Interfaces;

using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Models;

public interface IPageBlock
{
    Guid Id { get; }
    Guid PageId { get; }
    Guid? ParentBlockId { get; }
    BlockType Type { get; }
    int Order { get; }
    IBlockContent Content { get; }
    DateTime CreatedAt { get; }
    DateTime LastEditedAt { get; }
}
