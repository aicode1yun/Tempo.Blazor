using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Records a change to a block's <see cref="IBlockContent"/> so the edit
/// can be undone and redone without extra round-trips.
///
/// The command stores snapshots of <paramref name="before"/> and <paramref name="after"/>
/// content; <see cref="ExecuteAsync"/> applies <paramref name="after"/> and
/// <see cref="UndoAsync"/> restores <paramref name="before"/>.
/// </summary>
public sealed class UpdateBlockContentCommand : INotionCommand
{
    private readonly INotionBlockProvider _provider;
    private readonly List<IPageBlock>    _blocks;
    private readonly Guid                _blockId;
    private readonly IBlockContent       _before;
    private readonly IBlockContent       _after;

    public UpdateBlockContentCommand(
        INotionBlockProvider provider,
        List<IPageBlock>     blocks,
        Guid                 blockId,
        IBlockContent        before,
        IBlockContent        after)
    {
        _provider = provider;
        _blocks   = blocks;
        _blockId  = blockId;
        _before   = before;
        _after    = after;
    }

    public string Description => "Update block";

    public Task ExecuteAsync() => ApplyAsync(_after);
    public Task UndoAsync()    => ApplyAsync(_before);

    private async Task ApplyAsync(IBlockContent content)
    {
        var idx = _blocks.FindIndex(b => b.Id == _blockId);
        if (idx < 0) return;

        var existing = _blocks[idx];
        var updated  = new PageBlock
        {
            Id            = existing.Id,
            PageId        = existing.PageId,
            ParentBlockId = existing.ParentBlockId,
            Type          = existing.Type,
            Order         = existing.Order,
            Content       = content,
            CreatedAt     = existing.CreatedAt,
            LastEditedAt  = DateTime.UtcNow
        };

        await _provider.UpdateBlockAsync(updated);
        _blocks[idx] = updated;
    }
}
