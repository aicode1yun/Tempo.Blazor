using Tempo.Blazor.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Commands;

/// <summary>
/// Removes a block from the provider and from the local block list.
/// Undo re-creates the block (restoring its original content and position).
/// </summary>
public sealed class DeleteBlockCommand : INotionCommand
{
    private readonly INotionBlockProvider _provider;
    private readonly List<IPageBlock>    _blocks;
    private readonly string              _pageId;
    private readonly IPageBlock          _snapshot;     // full copy taken before deletion
    private readonly string?             _afterBlockId; // block that preceded it (for undo re-insertion)

    public DeleteBlockCommand(
        INotionBlockProvider provider,
        List<IPageBlock>     blocks,
        string               pageId,
        IPageBlock           block)
    {
        _provider  = provider;
        _blocks    = blocks;
        _pageId    = pageId;
        _snapshot  = block;

        // Record the preceding block's ID so undo can re-insert at the right position.
        var idx = blocks.IndexOf(block);
        _afterBlockId = idx > 0 ? blocks[idx - 1].Id.ToString() : null;
    }

    public string Description => "Delete block";

    public async Task ExecuteAsync()
    {
        await _provider.DeleteBlockAsync(_snapshot.Id.ToString());
        _blocks.RemoveAll(b => b.Id == _snapshot.Id);
    }

    public async Task UndoAsync()
    {
        var recreated = await _provider.CreateBlockAsync(_pageId, _snapshot, _afterBlockId);

        var afterBlock = _afterBlockId is null
            ? null
            : _blocks.FirstOrDefault(b => b.Id.ToString() == _afterBlockId);
        var insertIdx  = afterBlock is null
            ? 0
            : _blocks.IndexOf(afterBlock) + 1;

        _blocks.Insert(Math.Clamp(insertIdx, 0, _blocks.Count), recreated);
    }
}
