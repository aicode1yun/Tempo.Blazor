using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Changes a block's <see cref="BlockType"/> while preserving its position.
/// Undo restores the original type and content.
/// </summary>
public sealed class ConvertBlockCommand : INotionCommand
{
    private readonly INotionEditorBlockService _provider;
    private readonly List<IPageBlock>    _blocks;
    private readonly Guid                _blockId;
    private readonly BlockType           _fromType;
    private readonly IBlockContent       _fromContent;
    private readonly BlockType           _toType;
    private readonly IBlockContent       _toContent;

    public ConvertBlockCommand(
        INotionEditorBlockService provider,
        List<IPageBlock>     blocks,
        Guid                 blockId,
        BlockType            fromType,
        IBlockContent        fromContent,
        BlockType            toType,
        IBlockContent        toContent)
    {
        _provider    = provider;
        _blocks      = blocks;
        _blockId     = blockId;
        _fromType    = fromType;
        _fromContent = fromContent;
        _toType      = toType;
        _toContent   = toContent;
    }

    public string Description => $"Convert to {_toType}";

    public Task ExecuteAsync() => ApplyAsync(_toType, _toContent);
    public Task UndoAsync()    => ApplyAsync(_fromType, _fromContent);

    private async Task ApplyAsync(BlockType type, IBlockContent content)
    {
        var idx = _blocks.FindIndex(b => b.Id == _blockId);
        if (idx < 0) return;

        var existing = _blocks[idx];
        var converted = new PageBlock
        {
            Id            = existing.Id,
            PageId        = existing.PageId,
            ParentBlockId = existing.ParentBlockId,
            Type          = type,
            Order         = existing.Order,
            Content       = content,
            CreatedAt     = existing.CreatedAt,
            LastEditedAt  = DateTime.UtcNow
        };

        await _provider.UpdateBlockAsync(converted);
        _blocks[idx] = converted;
    }
}
